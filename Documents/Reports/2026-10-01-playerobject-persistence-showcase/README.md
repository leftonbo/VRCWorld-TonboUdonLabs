---
created_at: "2026-10-01T20:40:33+09:00"
updated_at: "2026-10-01T21:45:00+09:00"
category: report
tags: [vrchat, udon, persistence, showcase]
status: completed
experiment_id: po-persistence-showcase-v5
environment: "Windows / Unity 2022.3.22f1 / VRChat SDK 3.10.5 (bundled UdonSharp)"
---

# 値幅順に並べた200ケースで保存境界を比較する

`PlayerObjectPersistenceBatch`を、人に見せながら操作できる構成に整理した。
固定長18型は各8ケースで、1個の対照と、これまでの最大保持数を中心に前後3個を載せる。
以前の92ケースは残し、108ケースを追加した。
[21:21の実クライアント結果](client-results-20261001-212152.md)で全200ケースを照合し、固定長の境界が追加範囲でも再現した。146ケース保持、54ケース全値既定値だった。

## 固定長は1・2・4・8・12・16 Bの順、可変長と配列は後ろ

並びの「バイト長」は1フィールドの値部分の計算上の幅とした。
UIの「値B」は、その幅に個数を掛けた値。送信BやPlayerObject全体の使用量と区別する。

| 値幅 | 型 | 掲載個数 | ページ |
|---|---|---|---|
| 1 B | bool, byte, sbyte | 1, 61–67 | 1 |
| 2 B | short, ushort, char | 1, 40–46 | 2 |
| 4 B | int, uint, float, Color32 | 1, 23–29 | 3–4 |
| 8 B | long, ulong, double, Vector2 | 1, 12–18 | 5–6 |
| 12 B | Vector3 | 1, 7–13 | 7 |
| 16 B | Vector4, Quaternion, Color | 1, 5–11 | 8 |
| 可変長 | string, VRCUrl、JSON4096文字 | 従来の16ケース | 9 |
| 配列 | int[] | 従来の20ケース、最大1024要素 | 10 |
| 配列 | long[] | 同上 | 11 |

一覧は1ページ最大24ケースで、値幅の変わり目で改ページする。
説明板には操作手順、復元と現在の違い、これまでの実クライアントでの観測を記した。
初回の0一致は未保存として読む。再保存後も「復元」は入室直後の記録を保つ。

## 操作は一括保存、待機、Rejoin、復元の確認

1. `Assets/App/World/Scenes/PlayerObjectPersistenceBatch.unity`を同じワールドとして更新する。
2. 入室し、全200ケースの復元待ちが終わってから「一括保存」を1回押す。
3. 送信成功200/200と容量欄の更新を待ち、Rejoinする。
4. 保存を押さず各ページの「復元」を確認する。記録するときは結果コピーを押す。

保存とTSV出力は全ページが対象。ページ切り替えでは保存しない。
説明板の数値は[以前の92ケースの実クライアント観測](../2026-10-01-playerobject-persistence-variable-array/client-results-20261001-201041.md)に基づく。
string・VRCUrlは512フィールド、int[]・long[]は1024要素の保持まで観測済みで、上限は見つかっていない。
送信成功と容量更新はクラウド保存完了の通知ではない。

## 検証記録

Editor/ClientSimのUdon VMで、全200ケース・13,191値の型と値を照合した。
一括保存は各ケース1回、全11ページでケースの欠落・重複なし。ページ移動による保存もない。
全件の復元が揃う前の保存を拒否すること、入室直後の復元記録を保つこと、配列の明示保存時の修復も確認した。
TSVは200ケースを表示順に出力し、容量取得は管理クラスだけが入室後と全件送信後に行う。

| 証拠 | 内容 |
|---|---|
| [editor-validation.txt](editor-validation.txt) | 型・同期フィールド数・値数・Network ID・Program GUID・UI接続 |
| [editor-live-check.txt](editor-live-check.txt) | 13,191値の型と値、保存回数、復元記録、ページ操作 |
| [editor-export-storage-check.txt](editor-export-storage-check.txt) / [raw-editor-written.tsv](raw-editor-written.tsv) | 全件TSV・配列実長・使用量APIとUI |
| [identity-preservation.txt](identity-preservation.txt) | ワールドIDと残したケースのIDを保持、今回の退役ケースは0 |
| [matrix-identity-check.txt](matrix-identity-check.txt) / [check-showcase.py](check-showcase.py) | 個数範囲・値幅順・従来ケースと過去IDの照合 |
| [client-compile.txt](client-compile.txt) | クライアント向けUdonコンパイル。ワールドビルド・起動・アップロードは含まない |
| [editor-page-1.png](editor-page-1.png) / [editor-page-9.png](editor-page-9.png) / [editor-page-11.png](editor-page-11.png) / [editor-info.png](editor-info.png) | 一覧と説明板の表示確認 |

以前の92ケースの同期構成・Network ID・Program GUIDを照合し、新108ケースのIDが過去の退役IDを再利用していないことも確認した。
新規ソースは36本で、残り72本は既存のケースを再利用した。旧単独検証シーン2つと`PersistenceTyped26/27`のソースは変更していない。
既存のコンパイル生成物の変更は破棄せず、新規36組のソース・ProgramAsset・SerializedUdonProgramと各metaを追加した。

画像と生TSVは初回未保存から書き込んだClientSimの記録で、全ケースの復元一致数は0。
容量327523 / 102400 Bと容量超過表示もClientSimの記録であり、実クライアントの保存容量の測定値には使わない。
整備時のエージェントによる確認には、VRChat実クライアント検証、Build & Test、アップロードを含まない。
その後ユーザーから提供された実クライアントの結果は、上記の別レポートと[生TSV](raw-client-restored-20261001-212152.tsv)に記録した。
ClientSimでの再入室テストは、既知のSDK側char復元例外があるため、この作業の合格条件に含めていない。

```powershell
python -X utf8 Documents/Reports/2026-10-01-playerobject-persistence-showcase/check-showcase.py
```

Editorの動作確認は保存境界の実測とは分けて扱う。
ClientSimの容量表示も、実クライアントの使用量と同じ測定値として扱わない。

## セットアップ用エディタ拡張は削除し、保存済みシーンを使う

初期配置・型切り替え・実装中の確認に使った`PersistenceExperiment`、`PersistenceTypeExperiment`、`PersistenceBatchExperiment`と各metaを削除した。
シーンとランタイム実装、既存のNetwork ID・ProgramAsset・GUIDは保持する。
`UdonSharpProgramAssetAutoGenerator`は今後のUdonSharp追加にも使うため残し、既存の`SharedDataExperiment`も変更しない。

過去レポートの`Tools/PlayerObject Persistence/...`メニュー操作は、検証当時の記録である。
現在は保存済みシーンを開いて通常のSDK操作で使う。
`check-showcase.py`は削除したビルダーに依存せず、保存済みEditor記録の構成・値・IDを照合する。
実クライアント結果の照合は`analyze-client-results.py`で引き続き実行できる。

削除後のC#コンパイルはエラー・警告とも0で、検証シーンのMissing Script・Missing Prefabも0だった。
3つの保存済みシーンのハッシュは整理前と一致し、200ケースの記録照合も通過した。
新規304組のProgramAssetとSerializedUdonProgramは、対応するソース・meta・GUIDを揃えてコミット対象に含める。
既存生成物は、共有プローブの新しい参照・型情報と実装更新に必要なArray27・Fixed27・Scalars・Labの4プログラムに限定する。
Scalarsの28フィールド構成と既存シーンのワールドIDも維持する。その他の再コンパイル差分は作業ツリーに残す。
