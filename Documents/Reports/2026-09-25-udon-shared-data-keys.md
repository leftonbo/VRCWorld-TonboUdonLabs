---
created_at: "2026-09-25T23:45:00+09:00"
updated_at: "2026-09-25T23:51:22+09:00"
category: report
tags: [udon, performance, shared-data, localization, vrchat]
status: completed
experiment_id: UDON-SHARED-KEY-20260925
measured_at: "2026-09-25T23:47:51+09:00"
environment: "Unity 2022.3.22f1 / VRChat Worlds SDK 3.10.5 / Windows PC Desktop"
---

# UdonBehaviour 間の共有データと Dictionary キー型の性能検証

## 判断の要点

**固定型の共有配列は採用候補になるが、DataList を共有するだけでは通信が軽くなるとはいえない。**
PC Desktop 実機では、配列の個別通信が引数・戻り値方式より14～29%短かった。
DataList は同方式の0.96～1.06倍で、明確な改善が続かなかった。

**翻訳キーを全面的に int へ置き換える根拠にはならない。**
既存キーの取得は int が10～31%短くなったが、N=512・辞書1,024件の代表条件では1回あたり約0.235 μsの差だった。
100回なら約0.0235 ms相当という小ささであり、読みやすい string キーを基本にできる。
これは1回あたり中央値の単純換算で、100件のUI更新を実測した値ではない。
string→ID変換を毎回辞書で行うと、string のまま引く場合の1.30～1.47倍に増えた。
int 化を選ぶなら ID を事前に保持する。

Editor と実機でそれぞれ7,440行を取得し、全行のchecksumと追加の保持値検査が通った。
以下の短縮率は、4条件×2 runの各中央値を同条件どうしで比べた範囲であり、信頼区間ではない。

## 何を比較したか

ゲームの役割ごとに UdonBehaviour を分けたまま、同じクライアント内のデータ受け渡しを軽くできるかを調べた。
ネットワーク同期の実験ではない。
`SharedDataBenchmark` と別 GameObject の `SharedDataTarget` を使い、同じ入力 `i` に対する `i + 1` を送り手に返す。
配列と DataList は実行時に一度生成して渡し、測定中に複製しない。

| ケース群 | 測定区間に含む処理 |
| --- | --- |
| `comm_inline` | 境界を越えずに `i + 1` を合計する対照 |
| `comm_argument_return` | 引数付き外部メソッド呼び出し、戻り値の合計 |
| `comm_fields_direct/event` | 外部 field へ入力を書く、呼び出す、外部 field から結果を読む |
| `comm_array_direct/event` | 共有 `int[2]` へ入力を書く、受け手を呼ぶ、配列から結果を読む |
| `comm_list_direct/event` | 同じ処理を共有 DataList と DataToken の Int 値で行う |
| `comm_array/list_argument` | 事前生成した同じコンテナを、毎回メソッド引数として渡す |
| `comm_cached_array/list_write` | キャッシュした共有コンテナへ書く。受け手の通知・処理は含まない |
| `comm_external_field_write` | 受け手の field へ書く。通知・処理は含まない |
| `comm_array_batch` | N 要素への入力書き込み、受け手の一括処理を1回呼ぶ、送り手で全結果を合計する |

`direct` は UdonSharp の直接メソッド構文、`event` は同じ対象メソッドへの `SendCustomEvent`。
対象 Behaviour と公開メソッド数は全ケースで固定した。
共有参照の初期配布、対象の検索、コンテナの確保は通信の定常測定に含めない。
各バッチ前に受け手の配列参照を設定しているが、その再設定も計測外である。
したがって、初期化コストを何回の通信で回収するかは未測定。

Dictionary は、同じ整数値 `keyIndex + 1` を持つ2つの辞書を作り、キーだけを変えた。
string キーは `translation.menu.entry.0` から始まる24～27文字、int キーは0始まりの連番。
翻訳文章の描画時間を含めず、キー検索の差を切り出すため値は Int に統一した。

| ケース群 | 測定区間に含む処理 |
| --- | --- |
| `dict_string/int_get` | 既存キーの取得と Int 値の合計。string は生成済みの配列から取得 |
| `dict_string/int_token_get` | キーを DataToken に変換して保持済みの状態で取得 |
| `dict_string/int_tryget` | 必ず存在するキーを `TryGetValue` で取得。欠落・型不一致は対象外 |
| `dict_string/int_update` | 既存値を `keyIndex + 2` へ更新。計測後に全要素を検査して戻す |
| `dict_string/int_build` | デフォルト容量の辞書を new し、全キーを Add。string 生成は含めない |
| `key_string_generate` | 接頭辞と数値を連結し、配列に保存して文字数を合計 |
| `key_int_generate` | 数値 ID の生成、配列への保存、検算用の加算 |
| `dict_string_generate_get` | string キーの組み立てと検索を毎回行う |
| `dict_string_to_id_get` | string→int の辞書を引き、得た int で値の辞書を引く |

文字列生成と数値生成は仕事量も検算方法も異なる。
この2ケースの時間比を「ハッシュの速度比」として扱わない。
string の通常取得と int の通常取得にも、配列アクセスと整数演算の違いがある。
DataToken を用意したケースを併記し、純粋なハッシュ関数だけの比較とは区別する。

辞書件数が128の場合は同じキーを2～4周、1,024の場合は先頭256／512件を1周する。
順次アクセスであり、ランダム分布・長いキー・大規模辞書の一般則は測っていない。

## 測定と検算

- 反復数 N は256と512、辞書件数は128と1,024の4条件。構築ケースは辞書件数を操作数とする。
- 各条件で2 run。各ケース5バッチをウォームアップとし、30バッチを保存する。31ケース×4条件×2 run×30 = 7,440行／環境。
- 各バッチは1フレーム内で計測し、次のバッチは次フレームへ送る。ケース順はフェーズと run ごとに正順／逆順を交互にする。
- `Stopwatch.GetTimestamp()` の差を `Stopwatch.Frequency` で換算する。UdonSharp コンパイルと実機で使用可、実測で Frequency=10,000,000、IsHighResolution=True。
- `clock` は同じ dispatcher を呼ぶ空ケース。時計と dispatcher の費用を含むため、時計だけの裸の読み取りとは異なる。算術ループと構築用ループを別の対照にする。
- 共有配列／DataList へ送り手が値を書き、受け手が結果スロットを変更し、その結果を送り手で検査する。各条件の初期化で alias 検査を実施する。
- 取得と通信は全反復の合計、構築と更新は辞書の全要素、書き込み専用とキー生成は保持した値も検査する。ウォームアップ中の不一致も Failures に数える。
- 文字列化、CSV作成、ログ、辞書の内容検査は計測外。実機へのCSVログ出力は全測定が終わった後に行う。GC割り当て量と回収回数は未測定。

p95 は昇順に並べた標本の `ceil(0.95 × n)` 番目とする。
対照差分は同じ条件・run・バッチの `case_ms / operations - baseline_ms / baseline_operations` を求め、その中央値を取る。
キー文字列生成は同等の結果保持をする対照がないため、差分欄を空にする。
基準より小さい差や負の差も削除・ゼロ丸めしない。
時系列の外れ値を削除せず、最小・最大とともに残す。

## 再現環境

| 項目 | 条件 |
| --- | --- |
| Unity | 2022.3.22f1 (887be4894c44) |
| SDK / UdonSharp | VRChat Worlds / Base 3.10.5、同梱 UdonSharp |
| クライアント | VRChat 2026.3.3-1917-ee11595942-Release |
| OS / CPU / GPU | Windows 11 10.0.26200 / Core i7-9700K 3.60GHz / RTX 3070 Ti |
| 電源 | Windows「高パフォーマンス」。温度・動作クロックは未採取 |
| モード | PC Desktop、`--no-vr`、ローカル Build & Test、1クライアント |
| 表示設定 | 保存設定は1600×900 windowed、VSync無効、FPS制限無効。瞬間FPSは未採取 |
| アバター | 利用者の既定アバター。アバター間比較なし。個人識別子は生データへ転記しない |
| バックグラウンド | Unity Editor と Codex 等が稼働。OS全体の負荷を一定には固定していない |
| デバッグ | 実機は SDK 標準の debug GUI / SDK log levels / Udon debug logging 有効。Profiler 接続・Deep Profile 計測なし |
| Editor | Udon VM の値を `VRC.Udon.UdonBehaviour.GetProgramVariable` から取得。proxy C# の速度ではない |
| 起点 commit | `99d9beb`。今回追加した実験ファイルを適用。既存シーンと前回の性能ベンチを保全 |

シーンは `Assets/App/World/Scenes/SharedDataExperiment.unity`。
既存のワールドをコピーし、コピー側の `UdonPerformanceLab` を無効化した。
比較する二つの Behaviour は別 GameObject に置いた。
元のワールド、他のスクリプト、SDKパッケージには機能変更を加えていない。
Build & Test が更新した既存コンパイル生成物はコミット対象に含めない。

## 再現手順

1. 指定版の Unity で実験シーンを開く。Inspector の `AutoRun=true`, `Warmups=5`, `Batches=30` を確認する。
2. Editor 測定は Play。完了ログ後に `Tools > Shared Data Experiment > Export Editor Result` を実行し、停止する。
3. 実機は SDK パネルを開いて `Tools > Shared Data Experiment > Build And Test`。ツールがこの起動だけクライアント数を1へ変更し、SDK設定を戻す。既に動いている別クライアントは事前に終了する。
4. 起動から120フレーム後に自動開始する。`[SharedData] complete rows=7440` と、その後の `EXPORT_END rows=7440` を待つ。ログ出力の終了までクライアントを閉じない。
5. `python Documents/Reports/2026-09-25-udon-shared-data-keys/analyze.py --extract <output_log.txt> <raw-client.csv>` で抽出する。個人情報を含むクライアントログ全体はコミットしない。
6. `python Documents/Reports/2026-09-25-udon-shared-data-keys/analyze.py <raw-editor.csv> <raw-client.csv>` で検算と集計を再実行する。欠落行、重複行、checksum不一致を検出すると失敗する。

初回のシーン生成を再現する場合だけ、実験シーンが存在しない状態で `Tools > Shared Data Experiment > Create Scene` を使う。
未保存シーンがある場合は停止するので、保存または保全してから実行する。

## 予備測定と除外

SDK の既存設定による2クライアント同時起動はログが混在したため、実行を中断した。
全バッチのCSV出力前に停止したので、採用できる生データはない。性能集計には含めない。

1クライアントの予備測定は `pilot-raw-editor.csv` と `pilot-raw-client.csv` に全行を保存した。
この版の `comm_array_batch` は入力充填を計測外に置いていたため、最終版では充填も計測内へ移した。
また、最終版では書き込み専用・キー生成の保持値の検査を追加した。
予備測定は最終表と混ぜない。

## 実施段階

| C# compile | U# compile | Editor / ClientSim | VRChat 実機 |
| --- | --- | --- | --- |
| 成功、Console error 0 | Editor用・Build & Test用とも成功 | Udon VM、7,440行、Failures=0 | PC Desktop、7,440行、complete・EXPORT_END確認 |

## 結果

実機の代表条件（N=512、辞書1,024件、2 run 合計60標本／ケース）を示す。
全31ケースの条件別・run別集計は [実機集計CSV](2026-09-25-udon-shared-data-keys/summary-client.csv) と [Editor集計CSV](2026-09-25-udon-shared-data-keys/summary-editor.csv) を参照。
構築ケースだけは1バッチ1,024操作、ほかは512操作。
raw と対照差分は μs/op で示し、CSVには ms/op も保存した。

| ケース | 中央値 ms/batch | p95 | 最小～最大 | raw μs/op | 対照差分 μs/op |
| --- | ---: | ---: | ---: | ---: | ---: |
| `comm_inline` | 0.3701 | 0.6587 | 0.3463～0.9419 | 0.723 | 0.000 |
| `comm_argument_return` | 1.4831 | 1.9008 | 1.2988～2.3273 | 2.897 | 2.148 |
| `comm_fields_direct` | 1.5661 | 2.1557 | 1.3682～2.3775 | 3.059 | 2.269 |
| `comm_array_direct` | 1.1749 | 1.4722 | 1.0434～2.0290 | 2.295 | 1.520 |
| `comm_array_event` | 1.1340 | 1.6714 | 1.0016～1.9102 | 2.215 | 1.482 |
| `comm_list_direct` | 1.5614 | 2.2772 | 1.4046～3.7285 | 3.050 | 2.309 |
| `comm_list_event` | 1.4636 | 2.1744 | 1.3383～2.9224 | 2.858 | 2.116 |
| `comm_array_argument` | 1.5486 | 2.5049 | 1.3449～3.1031 | 3.025 | 2.229 |
| `comm_list_argument` | 1.9117 | 2.9216 | 1.6765～4.3787 | 3.734 | 2.949 |
| `comm_cached_array_write` | 0.4506 | 0.6589 | 0.4273～0.7728 | 0.880 | 0.166 |
| `comm_external_field_write` | 0.6472 | 1.1639 | 0.5989～1.4862 | 1.264 | 0.526 |
| `comm_cached_list_write` | 0.5489 | 0.8893 | 0.5101～1.0734 | 1.072 | 0.351 |
| `comm_array_batch` | 1.2472 | 1.8880 | 1.1180～2.1384 | 2.436 | 1.685 |
| `dict_string_get` | 0.7843 | 1.4478 | 0.7194～1.7221 | 1.532 | 0.629 |
| `dict_int_get` | 0.6642 | 0.8781 | 0.6103～1.2956 | 1.297 | 0.408 |
| `dict_string_token_get` | 0.7065 | 1.0182 | 0.6449～1.3802 | 1.380 | 0.460 |
| `dict_int_token_get` | 0.6561 | 0.9415 | 0.6171～1.3483 | 1.281 | 0.396 |
| `dict_string_update` | 1.1330 | 1.7654 | 1.0302～2.2723 | 2.213 | 1.320 |
| `dict_int_update` | 1.0122 | 1.4572 | 0.9351～3.4993 | 1.977 | 1.070 |
| `dict_string_build` | 1.7780 | 2.6890 | 1.6010～2.7221 | 1.736 | 0.978 |
| `dict_int_build` | 1.4969 | 2.1986 | 1.3810～2.4855 | 1.462 | 0.728 |
| `dict_string_generate_get` | 0.9780 | 1.5017 | 0.8756～3.2515 | 1.910 | 0.993 |
| `dict_string_to_id_get` | 1.1266 | 1.9098 | 0.9853～2.2523 | 2.200 | 1.256 |

共有配列は外部 field の往復に対しても16～36%短かった。
一方、一括配列方式は共有配列の個別呼び出しの0.97～1.11倍となり、この処理では一貫した優位が出なかった。
一括方式には入力・処理・合計の3ループがある。
呼び出し数の削減だけで、追加ループや配列操作を相殺できるとは限らない。

int キーは更新で5～15%、構築で5～23%短かった。
両方とも DataToken 配列を準備した取得比較でも、int 側が4～20%短かった。
ただし今回のCPUと入力分布の結果であり、キー型だけで桁違いに改善したという結果ではない。

`SendCustomEvent` と直接呼び出しでは、共有配列ケースの event/direct が0.84～1.05倍と条件で逆転した。
イベント構文への機械的な置換は勧めない。
実際の API の読みやすさと必要な通知方法を保ったまま、回数とデータ量を調整する。

Editor の絶対時間は実機より大きい。
異なる環境の数値を混ぜず、採用判断には実機表を使う。
例えば代表条件の実機では、引数・戻り値方式が約2.897 μs/op、共有配列が約2.295 μs/op。
1,000回の単純換算では約0.602 msの差だが、数回の呼び出しなら設計の明快さを優先しやすい。

時計対照の代表条件の中央値は6.1 μs/batch。
実処理ケースのバッチはこの対照より十分長く、時計のゼロ値はなかった。
それでもGCやOSの割り込みを除去していないので、p95・最大値とrun間の変動も確認する。

生データ: [実機](2026-09-25-udon-shared-data-keys/raw-client.csv) / [Editor](2026-09-25-udon-shared-data-keys/raw-editor.csv)。
実行証跡: [実機ログ抜粋](2026-09-25-udon-shared-data-keys/client-evidence.txt) / [Editor](2026-09-25-udon-shared-data-keys/editor-evidence.txt) / [ソース・bundleのSHA256と設定](2026-09-25-udon-shared-data-keys/reproducibility.txt)。
[集計・検算スクリプト](2026-09-25-udon-shared-data-keys/analyze.py) はPython標準ライブラリだけを使用する。


## 翻訳とゲーム設計へどう適用するか

役割分割をやめて巨大な Behaviour にまとめる前に、毎フレームの外部メンバー往復を数える。
固定型で頻繁に受け渡す小さな状態なら共有配列が候補になる。
スロット番号には定数名を付け、書く側・読む側・更新の順序を決める。
参照を渡した後に配列を置き換えると、受け手は古い参照を保持するので再配布が必要になる。
DataList の柔軟性は異種データや可変長が必要な場所で選び、共有するだけで速くなるとは見込まない。
共有コンテナは値を運ぶ手段であり、受け手への通知やネットワーク同期は別に必要になる。

翻訳の編集元は、意味が読める string キーを基本にする。
今回のキー差が、そのゲームの実際の翻訳更新回数で許容時間を超える場合に、読み込み時またはビルド時に安定した int ID を割り当てる。
毎フレームに翻訳文字列を取り直す処理なら、言語変更時や表示内容変更時に結果を保持する案も先に比べる。

int 化する場合は ID と意味名の対応を一元化し、重複・欠落・古いデータとの対応を検査する。
並び替えで ID が変わる採番や、衝突を扱わない `GetHashCode()` の値を永続 ID にする方式は避ける。
JSONへの出力で使える DataDictionary のキーは string に限られるため、データ交換用と実行時の表を分ける必要がある。[VRChat公式 Data Dictionaries](https://creators.vrchat.com/worlds/udon/data-containers/data-dictionaries/)

1回限りの変換表構築費、欠落キーの処理、多言語の切り替え、UIへの代入は今回の測定に含まない。
int 化の投資回収点は「変換・管理費」と「実ゲームでの呼び出し回数」も含めて判断する。
VRChat公式も Behaviour 間呼び出しの費用に注意を促しているが、公式の説明自体を今回の速度比の根拠にはしていない。[VRChat公式 Performance Tips](https://creators.vrchat.com/worlds/udon/udonsharp/performance-tips/)

PC VR、Android、複数プレイヤーの同期、異なるCPUでは未測定。
このレポートの結論は、今回のPC Desktop・SDK・キー長・アクセス分布・デバッグ条件の範囲で用いる。
