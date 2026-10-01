---
created_at: "2026-10-01T04:07:00+09:00"
updated_at: "2026-10-01T04:22:00+09:00"
category: report
tags: [vrchat, udon, persistence, field-count, primitive-types]
status: in-progress
experiment_id: po-persistence-types-26-27
environment: "Unity 2022.3.22f1 / VRChat SDK Worlds 3.10.5 / Editor ClientSim"
---

# PlayerObject Persistence: 型ごとの26・27フィールド比較

`long`・`float`・`double`・`bool`・`string` について、固定26フィールドと固定27フィールドを同じワールドで比較する。
初期状態は `long`。`int` に戻す対照メニューもある。
型ごとに新しいPrivateワールドを使い、型変更による既存保存データの移行と混同しない。

シーン: `Assets/App/World/Scenes/PlayerObjectPersistenceTypes.unity`

## ユーザーが行うこと

1. 新しいシーンを開く。まずは設定済みの `long` で、SDKから新しいPrivateワールドとして公開する。ワールド名に型を入れる。
2. 入室して「復元済み・書込み可」を待つ。最初は1人で行い、クライアント版・Steamブランチ・人数も控える。
3. seedは `101` のまま、「long 26 scalar」を選び「全フィールド書込＋送信」を押す。「期待値と比較」で **26 / 26 一致**、`post` の増加、`lastSuccess=True`、`bytes` を確認する。
4. 「long 27 scalar」でも同じ操作をする。**27 / 27 一致** と送信結果を記録する。`int[27] 対照` も同様に書き込む。比較数が26のままなら27に直す。
5. 最後の `post-serialization` から3分待つ。これは待ち時間を揃えるためで、クラウド保存完了の保証ではない。**退出前のTSVをExportして保管**する。
6. 別ワールドへ移動し、同じワールドへ戻る。復元後は書込みボタンを押さず、各ケースを選んで「読取のみ」「期待値と比較」を行う。26側・27側・配列側の一致数を控え、**復元後のTSVも保管**する。
7. 次の型へ進む。Unityの `Tools > PlayerObject Persistence > Types` で `float`、`double`、`bool`、`string` のいずれかを選ぶ。C#再コンパイル完了後、シーンが未保存なら保存し、同じ場所の `Compile and Finish Setup` を実行する。画面の型と26・27の表示を確認してから、手順1〜6を繰り返す。

型が変わると `Compile and Finish Setup` がこの新シーンのBlueprint IDを空にする。
SDKで既存の別型ワールドを選び直さず、新しいPrivateワールドとして公開する。
同じ型の再検証では同じワールドへ戻り、ソース、フィールド名、Network IDを変更しない。

## ケースと書込み値

| case | 構成 | 同期フィールド数 | 値の数 |
|---|---|---:|---:|
| 0 | 選択型のスカラー `V001`〜`V026` | 26 | 26 |
| 1 | 選択型のスカラー `V001`〜`V027` | 27 | 27 |
| 2 | `int[] Values` | 1 | 27 |

すべてManual Sync、`VRCPlayerObject`・`VRCEnablePersistence`付き。
書込み・送信はローカル所有者の `OnPlayerRestored` 後だけ許可する。
復元後の自動書込み・自動NULL初期化は行わない。

| 型 | seed=101の最初の値 | 書込み式（i=0から） |
|---|---|---|
| int／配列対照 | 10101 | `seed * 100 + i + 1` |
| long | 5000010101 | `5000000000L + seed * 100L + i + 1` |
| float | 10101.5 | `seed * 100f + i + 1.5f` |
| double | 5000010101.5 | `5000000000d + seed * 100d + i + 1.5d` |
| bool | True | 全フィールドTrue。seedを変えても同じ値 |
| string | s101-v001 | `s{seed}-v{3桁のフィールド番号}` |

未保存時の既定値は数値0、boolはFalse、stringは空文字。
書込み値はすべて既定値と異なる。
floatのseedは1〜10000、小数を正確に扱える範囲に制限する。他の型は1〜1000000。
送信byte数と再入室後の復元を別々に観察する。型間で同じbyte数になるよう値を調整する実験ではない。

## 記録欄

| 型 | 26: 送信bytes | 27: 送信bytes | 26: 復元一致数 | 27: 復元一致数 | 配列: 復元一致数 |
|---|---:|---:|---:|---:|---:|
| long | | | /26 | /27 | /27 |
| float | | | /26 | /27 | /27 |
| double | | | /26 | /27 | /27 |
| bool | | | /26 | /27 | /27 |
| string | | | /26 | /27 | /27 |
| int（再確認時） | | | /26 | /27 | /27 |

TSVの列構成は従来どおり。
`detail` に `valueType`・`valueCount` を追加し、従来の `ints` は互換性のため残す。
`post-serialization` の `bytes` は送信時の値、`usage-updated` の値は全PlayerObjectの使用量であり、意味が異なる。
`lastSuccess=True` はクラウド保存完了通知として扱わない。
クライアントログも残す場合は、入室から復元後の確認まで含むファイルを保管する。

## 今回の検証範囲

int／long／float／double／bool／stringの6型について、以下のEditor確認がすべて通った。

- 同期フィールド数が26・27、配列対照が1であること。実際のコンパイル後の型、Manual Sync、マーカー、UIの11イベント接続。
- ClientSimで生成されたローカルPlayerObjectの**Udon VM**に書き込み、26・27個すべての値・実行時型・期待値比較を確認。復元前の書込み防止、不正seedの拒否、「送信のみ」の経路も確認。
- `IsEditorBuild=false` でのUdonSharpコンパイル。Unityのワールドビルドは実行しない。

生データと確認記録はこのフォルダの `raw-editor-{type}.tsv`、`live-{type}.txt`、`validation-{type}.txt`、`client-compile-{type}.txt`。
Editor上の送信・保存はClientSimの挙動であり、実クライアントのbyte数・クラウド保存・再入室の成否は未検証。
Build & Test、VRChat起動、アップロードは実施していない。

Editorで再実行する場合は `Types > Play Fresh Editor Checks` を使い、復元後に `Run Live Editor Checks` を実行する。
この開始メニューは新シーン専用の `ClientSimStorage/PlayerObjects/PlayerObject_*_PlayerObjectPersistenceTypes.json` を `.before-{type}-{ticks}.bak` へバックアップし、新規状態で開始する。バックアップは削除しない。
ClientSimはシーン名を保存キーに使うため、Blueprint IDを空にするだけでは型間のローカル保存を分離できない。
通常のPlayでdoubleの保存データをboolとして復元した試行では、SDK内で `Attempted to access Double token as Boolean` が出て復元しなかった。この型変更によるEditor上の失敗を、今回調べる実クライアントの26／27境界として扱わない。
根拠はインストール済みSDKの `ClientSimPlayerObjectStorage.PlayerDataFilePath` と `ClientSimUdonEncodeDecode.Get`。

元の `PlayerObjectPersistenceExperiment.unity` とユーザーが28個に変更した `PersistenceScalars.cs` は保持する。
型比較は追加シーン・追加スクリプトで行う。共通の観測コードと既存UI生成処理だけを再利用する。

## 仮説の扱い

ユーザーの既存実測では、intスカラー26個は復元し、27個・28個では復元しない。送信bytesはそれぞれ144・148・152。
今回の実装はこの報告を前提にした次の比較であり、新しい実クライアント測定結果ではない。
また、現象がopen-beta以前からあるという報告を受け、beta固有とは断定しない。

26個という境界がアルファベット数と一致する点は仮説として残す。
こちらのフィールド名は数字を付けた `V001`〜`V027` で、保存側の内部識別方法は未確認。
異なる型でも同じ26／27の境界が出ればフィールド数に依存する可能性が高まるが、内部に26文字の識別子がある証明にはならない。
型によって境界が変わる場合は、値の型・サイズ・エンコード等の違いを次に調べる。

参考: [VRChat公式: Network Details](https://creators.vrchat.com/worlds/udon/networking/network-details/)
