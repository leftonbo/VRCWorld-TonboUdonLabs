---
created_at: "2026-10-21T07:19:53+09:00"
updated_at: "2026-10-21T13:10:03+09:00"
category: report
tags: [udon, performance, shared-array, data-list, inter-behaviour]
status: completed
experiment_id: UDON-INTERDATA-T174
measured_at: "2026-10-21T07:15:40+09:00"
environment: Editor-UdonVM
---

# 2つのUdon間で共有Array・DataListを使う効果

固定型の状態を頻繁に読み書きする場合、初期化時に参照を共有し、その参照を保持する方法は有効だった。
今回のEditor Udon VM実測では、HP・所持金に相当するintを2個書いて2個読む1処理が、外部fieldで6.698 μs、共有int[]で2.288 μs、共有DataListで3.697 μsだった。
共有int[]は約66%、DataListは約45%短い。
ただし、コンテナ参照を毎回外部Udonから取得すると、この利点は消えた。
状態比較で計時したのは呼び出し元1つの2書き込み・2読み出しであり、受け手の保持値確認は計時外に行う。
両UdonがUpdate等から交互に書く場合の通知・更新順序の費用までは含まない。

引数2個と戻り値1個を持つ外部メソッドの比較では、通常呼び出し8.103 μsに対して共有int[]が5.235 μs、共有DataListが7.363 μsだった。
Arrayの改善は約35%、DataListは約9%であり、型付き配列とDataListを同じ最適化として扱えない。
共有Arrayによる一括処理は、入力充填と結果集計を含めると個別呼び出しより約11%遅かった。
これらは単一PCのEditor測定であり、実クライアントでの改善率は未確認である。

## 今回の比較範囲

[2026-09-25の共有データ実験](2026-09-25-udon-shared-data-keys.md)には、引数1個・戻り値1個、共有コンテナによる呼び出し、書き込み専用の比較がある。
今回はその成果を保存し、状態の読み書きにfield・property・methodを揃えた比較、参照の毎回取得、共有の準備費用を追加した。
過去のDictionary単体アクセスの結果から、今回の2Udon間通信の優劣を推定していない。

仮説の判断は次のとおり。

| 仮説 | 今回の判断 | 成立条件・限界 |
| --- | --- | --- |
| まとまった情報はpublic fieldより事前共有コンテナが軽い | **成立：今回のEditor条件** | int[]、DataListとも参照を保持した場合。読み書きだけを比較し、通知は不要な条件 |
| property・対応methodより事前共有コンテナが軽い | **成立：今回のEditor条件** | 単純なgetter/setterとの比較。実際のsetterが持つ検査や通知を省いてよいという意味ではない |
| 引数・戻り値より共有Arrayが軽い | **成立：今回のEditor条件** | 同じ計算を受け手で毎回実行。事前共有し、毎回は参照を渡さない |
| 引数・戻り値より共有DataListが軽い | **限定的** | 今回の引数2個では約9～10%短い。過去の実機・引数1個では一貫した優位がなかった |
| 共有Arrayなら一括化でさらに軽くなる | **不成立：今回の実装** | 3本のループ、入力・出力配列へのアクセスと添字計算が呼び出し削減を上回った |
| 実VRChat clientでも今回と同じ比率になる | **測定不足** | 今回の実行層はEditor Udon VM。PC VR・Quest・多人数・長時間は未検証 |

## 実装と再現手順

| 成果物 | 役割 |
| --- | --- |
| [InterUdonDataBenchmark.cs](../../Assets/App/World/UdonSharp/InterUdonDataBenchmark.cs) | 呼び出し元、タイマー、32条件、検算、CSV保持 |
| [InterUdonDataTarget.cs](../../Assets/App/World/UdonSharp/InterUdonDataTarget.cs) | 別GameObject上の受け手、状態・計算・共有参照の検査 |
| [InterUdonDataExperiment.cs](../../Assets/App/Editor/InterUdonDataExperiment.cs) | 専用シーン作成、U#コンパイル、live Udon heapからの結果取得 |
| [InterUdonDataExperiment.unity](../../Assets/App/World/Scenes/InterUdonDataExperiment.unity) | 再利用する実験シーン。既存の共有データ実験をコピーし、既存Udonは非アクティブ化 |
| [analyze.py](2026-10-21-udon-interdata-t174/analyze.py) | 全行のchecksum・条件・欠落・重複・順序検査、集計、時間比の区間推定 |

1. Unity 2022.3.22f1で本プロジェクトを開く。未保存シーンを保全し、Edit Modeとコンパイル終了を確認する。
2. 既存の `InterUdonDataExperiment.unity` を開く。再生成する必要はない。新しいcheckoutでシーンを初めて作る場合だけ `Tools > Inter Udon Data > Create Scene` を使う。既存シーンがある場合や未保存シーンがある場合は停止する。
3. `Tools > Inter Udon Data > Compile` を実行し、ConsoleにC#・U#のエラーがないことを確認する。初期設定はAutoRun=true、Warmups=5、Batches=30、Targetは別GameObject上のInterUdonDataTarget。
4. Play Modeに入る。120フレーム後に開始し、1フレームに1条件を実行する。途中確認は `Tools > Inter Udon Data > Live Status`。完了まで約6,720測定フレーム＋開始待機が必要で、Editorの実際の更新速度によって所要時間は変わる。
5. `Status=complete / Rows=5760 / AliasPassed=True / Failures=0` を確認し、`Export Editor Result` を実行する。失敗または未完了なら書き出しを拒否する。同じ保存先を上書きするため、別セッションの結果を残す場合は実行前に出力先を変えるか既存CSVを退避する。
6. Play Modeを終了し、`python Documents/Reports/2026-10-21-udon-interdata-t174/analyze.py` を実行する。標準ライブラリだけで集計できる。上記設定と異なるバッチ数等を使った場合は、検算スクリプトの期待条件も合わせる。

実クライアントで再測定する場合は、専用シーンをSDK Build & Testで起動する。
今回、その操作は実施していない。
Udonは計時後に `[InterUdonCSV]` の行を1フレームに1行ずつログ出力するので、`EXPORT_END rows=5760` まで待ち、単一クライアントのログからCSVを抽出する。
この出力処理には追加フレームが必要である。現在のanalyze.pyはEditor系列を検算するため、実機用には環境名を区別して別系列として処理する。

## 共有成立と測定の条件

開始時に、呼び出し元がint[]とDataListを作り、受け手のpublic fieldへ参照を渡す。
呼び出し元で17・23を書き、受け手が40を返すことを確認する。
続いて受け手が各値に7・11を加え、呼び出し元から24・34を読めることを確認する。
この往復が両コンテナで成立してから計測に入る。

[VM実行証跡](2026-10-21-udon-interdata-t174/vm-evidence.txt)には、`VRC.Udon.VM.UdonVM:Interpret()` と `UdonBehaviour:RunProgram` を通るログがある。
[Editor取得証跡](2026-10-21-udon-interdata-t174/editor-evidence.txt)は、異なる2つのUdonBehaviourのprogram名と完了状態をlive heapから読み取ったもの。
proxyのC#フィールド値や通常MonoBehaviourでの計時を、今回の実測に使っていない。
各共有状態・外部状態ケースの計時後にも、受け手から最終値を読んで検査している。

| 項目 | 条件 |
| --- | --- |
| ソース基準 | branch main、HEAD `bd824e4426c03e0e26b05098d3eba4d58ee9ba5b` ＋今回の未コミット追加。開始時はstaged/unstaged/untrackedなし |
| Unity / SDK / U# | Unity 2022.3.22f1、Worlds/Base SDK 3.10.5、U#は同SDK同梱。program/compiledVersion=2はアセット形式でありU#の製品版番号ではない |
| 補助ツール | UnitySkills 2.8.4、native Windows、PowerShell 7。接続先port 8090、project/pathとPIDを照合 |
| OS / CPU / GPU | Windows 11 build 26200、Core i7-9700K 3.60 GHz、GeForce RTX 3070 Ti |
| 実行層 | Editor Play ModeのUdon VM。ClientSim固有のプレイヤー操作は検証していない。人数・アバターは実クライアント比較の管理条件として未測定 |
| 表示 / 電源 | Game View 1220×687、TargetFPS=72、VSync=0、高パフォーマンス電源プラン |
| 負荷 | MassiveMedalPusherの別Unity EditorとCodex等が起動。CPU占有率・温度・クロック・OS割り込みは統制・記録していない |
| 時計 | Stopwatch.GetTimestamp、Frequency=10,000,000 Hz、IsHighResolution=True、tick換算0.1 μs。実効精度を0.1 μsと断定しない |
| 回数 | N=1/128/512、各5 warmup＋30採用バッチ、2 run。runは同じPlayセッション内で連続実行 |
| 順序 | バッチとrunごとに正順／逆順を交互にする。32条件×3 N×2 run×30=5,760行 |
| 計時範囲 | Runの分岐・ループ・演算・データ搬送・checksum加算を含む。参照初期化、検算、文字列化、CSV追加、ログ、通知スケジューラは範囲外 |
| 割当 | allocateケースだけはnewと初期要素追加を含む。ほかは事前確保。ただしCSV行生成等はバッチ間で割当を行う |
| Profiler / Deep Profile | 両方無効。常時ログなし、開始条件と完了ログのみ。GC bytes/collection countは未測定 |
| 外れ値 | 全行採用。黙った除外なし。最小・最大・run別中央値は集計CSVに保持 |

1処理の仕事量は、状態比較では `hp=i, money=i+1` の2書き込み＋2読み出し＋加算。
call比較では引数 `i, i+1` に対する `hp*3+money` の計算＋結果の加算である。
期待checksumは状態がN²、callが2N²−N。setupはN、clockは0。
呼び出し元の条件分岐は反復ループの外に置いている。
計算自体は受け手のループ内で明示してあり、搬送方法間で新たな本体helper呼び出しを加えていない。

## 状態の読み書きは参照保持の効果が大きい

N=512、2 run合計60標本。時間はすべて **μs/処理**。
p95もバッチ時間をNで割った値であり、個々の1処理のレイテンシ分布ではない。
対照差分は、同じconfig/run/batchのstate_inlineとの差を取った中央値。
負の小さな差は優位と解釈しない。

| 方式（CSV case） | 中央値 | p95 | 対照差分 | 実際の境界越え／処理 |
| --- | ---: | ---: | ---: | --- |
| inline | 1.401 | 1.617 | 0.000 | なし、local変数 |
| local_fields | 1.390 | 1.620 | -0.002 | なし、同一Udonのfield |
| local_property | 2.680 | 2.952 | 1.268 | なし、同一Udonのgetter/setter計4回 |
| local_methods | 2.684 | 3.031 | 1.284 | なし、同一Udonのmethod計4回 |
| local_array | 2.283 | 2.460 | 0.883 | なし、共有していないint[] |
| local_list | 3.666 | 4.028 | 2.290 | なし、共有していないDataList |
| external_fields | 6.698 | 7.519 | 5.338 | SetProgramVariable 2回＋GetProgramVariable 2回 |
| external_property | 18.275 | 20.147 | 16.826 | 外部setter 2回＋getter 2回。引数・戻り値の搬送も含む |
| external_methods | 16.777 | 18.669 | 15.382 | 外部set method 2回＋get method 2回。引数・戻り値の搬送も含む |
| shared_array | 2.288 | 2.619 | 0.893 | 保持したint[]の要素操作。処理中の境界越え0 |
| shared_list | 3.697 | 4.617 | 2.334 | 保持したDataListの要素操作。Int変換を含む。境界越え0 |
| refetch_array | 7.881 | 9.217 | 6.492 | 各要素操作前の外部参照取得、計4回 |
| refetch_list | 9.369 | 10.694 | 7.987 | 各要素操作前の外部参照取得、計4回 |

CSVでは上のcase名に `state_` が付く。
shared_arrayとlocal_arrayの時間比は1.002、95% bootstrap区間は0.978～1.027。
shared_listとlocal_listは1.008、区間は0.991～1.035であり、共有そのものの追加負荷は識別できない。
local_fieldsとinlineの差もばらつきの範囲である。
一方、参照を毎回取得する方式は保持した方式の3.44倍／2.53倍だった。
改善の中心は「共有」という名称ではなく、継続アクセス中の外部変数取得をなくすことにある。

## 外部処理の呼び出しでは搬送と通知を分ける

同じN・標本数・単位。対照はcall_inline。
個別ケースは各処理につき受け手の計算を1回、batchケースはN件を1回の通知で計算する。
入力の充填と結果の集計を含め、計算を省いて速く見せる比較にはしていない。

| 方式（CSV case） | 中央値 | p95 | 対照差分 | 搬送・呼び出し |
| --- | ---: | ---: | ---: | --- |
| inline | 1.536 | 1.759 | 0.000 | 呼び出し元で同じ式を計算する対照 |
| local_method | 1.917 | 2.369 | 0.374 | 同一Udon内、引数2個＋戻り値1個 |
| argument_return | 8.103 | 9.271 | 6.529 | 別Udon、引数2個設定＋イベント1回＋戻り値取得 |
| fields | 8.219 | 9.283 | 6.646 | public fieldに2入力、引数なし外部method1回、結果field取得 |
| array | 5.235 | 5.874 | 3.696 | 事前共有int[]に2入力、引数なし外部method1回、結果slot取得 |
| list | 7.363 | 8.382 | 5.850 | 事前共有DataListに2入力、引数なし外部method1回、結果slotをIntで取得 |
| array_event | 4.870 | 5.771 | 3.326 | arrayと同じ搬送、明示的SendCustomEvent |
| list_event | 6.976 | 8.439 | 5.385 | listと同じ搬送、明示的SendCustomEvent |
| array_argument | 6.940 | 8.637 | 5.418 | 毎回配列参照も引数で渡す |
| list_argument | 9.378 | 10.699 | 7.860 | 毎回DataList参照も引数で渡す |
| array_batch | 5.811 | 6.685 | 4.251 | N件の入力充填→外部イベント1回→N件の結果集計。計算N回 |

CSVではcase名に `call_` が付く。
array/argument_returnの時間比は0.646、95%区間0.627～0.666。
list/argument_returnは0.909、区間0.878～0.931。
N=128でも時間比は0.647／0.898で、run別にも同じ方向だった。
区間は60バッチを対にしたbootstrap 2,000回、seed=174で求めた。時間相関や別セッション差を保証する区間ではない。

参照を毎回引数で渡すと、事前共有に対してArrayで約33%、DataListで約27%長くなった。
array_eventはarrayより約7%短いが、どちらもUdonのローカルイベントを呼ぶ。
生成コードでは直接構文の一部に受け手参照のCOPY等が入るため、同じAPI形に見えても命令列は完全には一致しない。
公開method数、型、SDK版、実行層を変えた際の差は未確認なので、コード全体をSendCustomEventへ機械的に置換する根拠にはしない。

array_batch/arrayは1.110、区間1.066～1.131。
呼び出しをN回から1回へ減らしても、今回の追加の配列読み書き・3ループ構成では相殺できなかった。
同じArrayをそのまま処理できる既存tableや、合計値だけを渡せる場面とは条件が異なる。

## 初期化費用と単発の限界

N=512で反復したwarm状態の費用。setup_baselineとの差分にはループ対照を使う。
これは起動直後のcoldな1回を測った値ではない。

| 方式（CSVはsetup_付き） | 中央値 μs/回 | p95 | 対照差分 |
| --- | ---: | ---: | ---: |
| baseline | 0.826 | 0.906 | 0.000 |
| attach_array：既存参照を相手fieldへ渡す | 1.942 | 2.196 | 1.133 |
| attach_list：既存参照を相手fieldへ渡す | 1.987 | 2.239 | 1.142 |
| acquire_array：相手fieldから取得し保持する | 2.169 | 2.564 | 1.363 |
| acquire_list：相手fieldから取得し保持する | 2.215 | 2.686 | 1.373 |
| allocate_array：int[3]を確保する | 1.129 | 1.318 | 0.291 |
| allocate_list：DataListを確保し0を3個追加する | 2.904 | 3.485 | 2.075 |

共有状態の準備では片方が確保して参照を渡せばよく、attachとacquireを必ず両方行う必要はない。
運用上の損益分岐は `追加初期化時間 / 継続1処理あたりの短縮時間` が目安になる。
ただし、今回の反復した準備費用から、cold起動での正確な回収回数は決めない。

時計対照はN=512で中央値5.5 μs/batch、p95 6.3 μs/batchだった。
主要ケースの512件バッチは約0.7～9.4 msであり、時計対照より十分長い。
N=1では同じ時計対照だけで5.5 μsとなり、inlineの8.1 μsにも大きく寄与する。
単発の細かな差を実用上の高速化率として使わず、N=128/512の傾向を採用判断の中心にした。

## 生成コードが説明する範囲

[導入SDKの外部呼び出しコンパイラ](../../Packages/com.vrchat.worlds/Integrations/UdonSharp/Editor/Compiler/Binder/BoundNodes/BoundUdonSharpBehaviourInvocationExpression.cs)と[外部fieldアクセス](../../Packages/com.vrchat.worlds/Integrations/UdonSharp/Editor/Compiler/Binder/BoundNodes/BoundFieldAccessExpression.cs)、[実際の生成UASM抜粋](2026-10-21-udon-interdata-t174/assembly-evidence.txt)を照合した。
引数付き外部methodは、受け手の引数slotへSetProgramVariableし、SendCustomEventし、戻り値をGetProgramVariableで回収する。
一方、共有Arrayは保持した配列のSet/Getで搬送し、受け手へのイベントは残る。
共有Arrayが速い理由を「メソッドの処理や通知がなくなるため」と解釈してはいけない。

DataListの計時には、intからDataTokenへの変換、setter/getter、`.Int`での取得が含まれる。
実際のGC割当量・boxing件数を測っていないため、ArrayをGCゼロ、DataListを必ず割当が起きる方式と断定しない。
[公式DataList資料](https://creators.vrchat.com/worlds/udon/data-containers/data-lists/)も、DataTokenを保持するコンテナとして説明している。
[公式性能ガイド](https://creators.vrchat.com/worlds/udon/udonsharp/performance-tips/)の外部呼び出しや結果保持の注意は設計の出発点とし、数値は今回の実測から求めた。

## 過去の実機測定と今回の差

2026-09-25の保存済みraw-client.csvを今回読み直し、7,440行すべてのchecksum_ok=1と、代表条件の下表の中央値を再計算した。
実機は当時のVRChat build `2026.3.3-1917-ee11595942-Release`、PC Desktop・1クライアントである。
今回新たに実機を起動した結果ではない。

| 過去の実機ケース（引数1個、value+1） | 中央値 μs/処理 |
| --- | ---: |
| comm_argument_return | 2.897 |
| comm_array_direct | 2.295 |
| comm_list_direct | 3.050 |
| comm_array_batch | 2.436 |

過去の代表条件でもArrayは短いが、DataListは通常の引数・戻り値より長い。
今回の引数2個では搬送を減らす余地も増えるため、DataListのわずかな優位は汎用的な結論にしない。
引数数・計算式・実行層が異なるので、前後の絶対時間からSDKやPCの速さの変化を論じない。

## MassiveMedalPusherへ適用する際の優先順位

今回、参照先プロジェクトは読み取りだけに留めた。
参照HEADは `182ca3d0aba14118601e9f17dd938479f30eb56e`。以下は作業ツリー上のソースを読んだ観察であり、そのプロジェクト全体の性能測定ではない。

ルーレットの[強制抽選ループ](G:/MagdraTonbo/Contribute/World-MassiveMedalPusher/Assets/App/World/UdonSharp/Controller/item_slot.cs:374)は、1フレーム最大5,000件を処理し、Slot_Remainingをローカルに保持している。
開始・Fever消費・当たり統計は既にBulk単位で通知され、[当たり統計のBulk受け口](G:/MagdraTonbo/Contribute/World-MassiveMedalPusher/Assets/App/World/UdonSharp/Controller/DekapuGameManager.Gameplay.cs:299)もある。
次に調べる候補は、ループ中の `_ProcessResult` から繰り返される払い出し等の外部呼び出しである。
既存table内で同種の計算を済ませる案、型付きbufferに必要な入力だけ保持する案、件数や合計値だけを通知する案を比べる。
Fever消費順、乱数列、chain、抽選結果の順序、実績判定時点を保つ必要がある。

ボール獲得では、[MedalDropTrigger.Start](G:/MagdraTonbo/Contribute/World-MassiveMedalPusher/Assets/App/World/UdonSharp/Props/MedalDropTrigger.cs:85)が既に共有DataList `BallDropQueue` の参照を保持している。
[登録側](G:/MagdraTonbo/Contribute/World-MassiveMedalPusher/Assets/App/World/UdonSharp/Props/MedalDropTrigger.cs:152)は1レコードにitemId、chain、位置3値、compressionの6 tokenを追加し、[受け手](G:/MagdraTonbo/Contribute/World-MassiveMedalPusher/Assets/App/World/UdonSharp/Controller/DekapuGameManager.Ball.cs:75)が順番に処理してRemoveRangeする。
今回の参照保持の利点は既に取り込まれているため、キューを共有しただけの改善をもう一度見込まない。
追加検討するなら、既存キューの読み書き・Int/Float変換と、獲得本体の外部UI・音・レベル通知のどちらが支配的かを先に測る。
固定長／型付きの複数配列へ変える案には、intとfloatを保つ設計、容量不足、キュー追加中の処理、セッションリセット時のClearと参照維持が必要になる。
今回のint2個のベンチだけで、その異種6要素キューを置換する判断はできない。

既存の[通常ボール払い出し表](G:/MagdraTonbo/Contribute/World-MassiveMedalPusher/Assets/App/World/UdonSharp/Controller/DekapuGameManager.Ball.cs:290)はchainごとの計算結果を保持している。
このような局所的な結果保持を先に活用し、1球ごとの由来追跡・保存・同期項目を追加する構造変更は前提にしない。
圧縮されたball数、整数丸め、JP・Credit等の別扱い、上限処理を崩さず、変更前後で同一入力の獲得結果を比較する必要がある。

今回の単純換算では、外部fieldから共有Arrayへの差は約4.41 μs/状態処理、引数・戻り値から共有Arrayへの差は約2.87 μs/計算処理である。
1,000回なら約4.41 ms／2.87 msに相当するが、これは今回のEditor microbenchmarkを線形換算した見積もりであり、MassiveMedalPusherのフレーム改善実測ではない。
少数の呼び出しなら、APIの明快さや変更の局所性を優先しやすい。

## 採用時に必要な約束

- 初期化完了を確認してから参照を配る。Startの順序だけに依存せず、必要なら明示的な登録や準備済みフラグを使う。
- 共有後は同じinstanceを維持する。配列／DataListをnewで差し替えたら、全保持先へ再配布する。Clearと参照置換は別操作である。
- HP・所持金・引数・結果のslot番号に名前を付け、要素の型、長さ、書く側、読む時点を一元管理する。DataToken.Int取得は正しい型を前提とする。TryGet等の追加検査費用は今回は測っていない。
- 状態を直接書く方式で、setterの検査・UI更新・通知・保存・同期を迂回しない。必要な副作用は別途同じ頻度で実行して比較する。
- 引数／結果bufferは同期的な1呼び出しの間だけ占有する。ネスト呼び出し、受け手からのcallback、複数呼び出し元で同じslotを使う処理では上書きが起こり得る。呼び出し順を制約するか、呼び出し元別buffer等の小さな対策を選ぶ。
- 本実験はローカルな共有参照で、両新規BehaviourのsyncはNone。別クライアントへの複製、ownership、late join、Persistenceは別設計である。

## 検証範囲と保存データ

| 段階 | 今回の結果 |
| --- | --- |
| C#コンパイル | 成功 |
| UdonSharpコンパイル | 成功。Editor用の実際のprogramを実行 |
| Editorの実Udon VM | 完了、5,760行、Failures=0、共有成立、Console error/exception 0 |
| 検算 | checksum期待式、行数、全32条件、N、batch/run、重複、欠落、正逆順序を検査して成功 |
| Editor authoring静的検査 | `check_editor_sources.py Assets` の新規実験に該当する指摘0件。既存コードのcopy呼び出し4件は変更せず、実行時動作の証明とは分ける |
| 今回のSDK Build & Test / 実VRChat client | 未実施 |
| PC VR / HMD / Quest / 多人数 / 長時間 / GC指標 | 未実施 |

[生CSV](2026-10-21-udon-interdata-t174/raw-editor.csv)、[全条件集計](2026-10-21-udon-interdata-t174/summary-editor.csv)、[時間比と区間](2026-10-21-udon-interdata-t174/ratios-editor.csv)、[ソースhash・作業状態](2026-10-21-udon-interdata-t174/reproducibility.json)を保存した。
巨大なEditorログ全体はLibrary内に置き、共有成果にはVMスタックと完了ログの抜粋だけを含めた。
SDK再コンパイルで変わった既存SerializedUdonProgramsは作業ツリーに保持し、新規実験のprogram資産と区別している。
既存生成物をrestoreや一括stageしていない。今回の実験コード・シーン・測定結果は承認後にまとめてコミットし、既存生成物の再コンパイル差分は含めない。push・SDK uploadは行っていない。
