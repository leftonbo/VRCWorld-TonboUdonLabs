---
created_at: "2026-10-01T19:53:00+09:00"
updated_at: "2026-10-01T20:13:09+09:00"
category: report
tags: [vrchat, udon, persistence, array, storage-usage]
status: completed
experiment_id: po-persistence-variable-array-v4
environment: "Unity 2022.3.22f1 / VRChat SDK Worlds 3.10.5"
---

# string・VRCUrlの129フィールド以上とint・long配列を比較する92ケース

シーンは `Assets/App/World/Scenes/PlayerObjectPersistenceBatch.unity`。
[前回の実クライアント結果](../2026-10-01-playerobject-persistence-all-types/client-results-20261001-142619.md)で確認した固定長18型の境界2ケースずつを残し、string・VRCUrlと配列を増やした。
全92ケース。既存41ケースの同期スキーマ・保存用Network ID・プログラムGUIDとワールドIDを維持し、51ケースを追加した。
70ケースをシーンから外し、そのソース・ProgramAsset・過去の結果は残す。追加ケースに整理前の保存IDを再利用しない。

20:10 JSTの[実クライアント結果](client-results-20261001-201041.md)を追記した。
string・VRCUrlは512フィールドまで、int[]・long[]は1024要素まで、配置した全ケースで保持を確認した。
固定長スカラーの境界は前回と一致。API取得値は49640 / 102400 Bで、取得時点では上限未達だった。

## 次の操作

1. 同じPrivateワールドへ更新アップロードして入室する。
2. 「一括保存」を1回押す。送信成功92/92と、容量欄の「更新待ち」が消えることを確認する。
3. Rejoinし、保存を押さず「結果をコピー」を押す。結果作成が完了すると全ケースのTSVが出る。

送信成功・容量の更新とも、クラウド保存完了通知ではない。保持の判定にはRejoin後の復元値を使う。

## 配置した個数

| 対象 | 個数 |
|---|---|
| string・VRCUrlの同期フィールド数 | 各128, 129, 130, 255, 256, 257, 512 |
| int[]・long[]の要素数 | 各1, 15, 16, 26, 27, 28, 31, 32, 33, 63, 64, 65, 127, 128, 129, 255, 256, 257, 512, 1024 |
| 既存対照 | string27フィールド、JSON4096文字1フィールド、固定長18型の境界2ケースずつ |

配列は各ケースに `[UdonSynced]` のフィールドを1個だけ宣言し、要素数を変える。
int[27]の旧ケースも維持。配列フィールドの個数を変える検証ではない。
stringは従来のseed=101の文字列、VRCUrlはEditorで用意した独立URLオブジェクトを使う。
int配列は10101から、long配列は5000010101からの連番にする。
初期化済みの配列を配置し、復元値は自動で修復しない。明示的な保存時だけ、NULLや長さ不一致の配列を指定要素数へ戻して書き込む。
TSVに `current_shape` 列を追加し、配列の実際の長さやNULLも確認できるようにした。

今回の上端はstring・VRCUrlが512フィールド、配列が1024要素。全部保持された場合はその範囲での保持確認であり、上限を確定したことにはならない。

## 使用量は管理クラスから全体を取得

`PersistenceBatchLab` 1個で `Networking.GetPlayerObjectStorageUsage(Networking.LocalPlayer)` と `Networking.GetPlayerObjectStorageLimit()` を呼ぶ。
[公式PlayerObjectドキュメント](https://creators.vrchat.com/worlds/udon/persistence/player-object/)に従い、対象はローカルプレイヤーのPlayerObject全体の最後に計算された使用量。ケース別の保存量ではない。
UIには取得した値をそのまま「使用量 / 上限 B」で表示する。ケースの送信Byte数を合算して使用量と見なさない。

全ケースの復元がそろった時と、一括送信結果が全件そろった後に、管理クラスだけが `Networking.RequestStorageUsageUpdate()` を要求する。
短時間の要求は10秒以上あけ、毎フレーム・毎ケースの要求はしない。
`OnPersistenceUsageUpdated()` で再取得し、更新待ちを解除する。
TSVのコメントにも全体の使用量・上限・更新待ち状態・取得日時を出力する。
使用量を更新しても、送信成功を永続保存完了として扱わない。

## Editorの確認と実クライアントの確認は分ける

構成とID保全は `editor-validation.txt`、`identity-preservation.txt`、`retired-cases.tsv` に記録する。
再現メニューは `Tools > PlayerObject Persistence > Batch`。
`Play Fresh Editor Check` → 復元待ち → `Begin Live Editor Check` → 書込み後に `Finish Live Editor Check` → 結果作成完了後に `Check Live Export`。
既存の一括保存・ページ表示の検証に加え、int[]のNULLとlong[]の長さ0を明示保存で修復する経路も確認する。
結果作成は1ケース1フレームに分け、全ページを1回の操作で出力する。

ClientSimの使用量APIはSDK自身が正確でないと警告する。EditorではAPIの返り値・イベント・UI・TSVの配線を確認し、実クライアントの保存量として扱わない。
今回のClientSim取得値は247905 / 102400 Bで容量超過イベントも表示されたが、これを実クライアントの使用量や超過の証拠にはしない。
実クライアントで上限を超えた場合は、全体容量の問題と個別ケースの境界を分けて判定する。
固定長型の対照にcharを残しているため、既知のClientSim char復元例外も引き続き関係する。SDKには変更を加えない。
実クライアントでの追加ケースの保持結果は上記レポートに記録した。以下は実装時のEditor側の確認記録。

今回の確認結果:

- [構成確認](editor-validation.txt)と[ID保全](identity-preservation.txt): 全92ケースの同期型・フィールド数、Manual Sync、Persistenceマーカー、UI配線、保存IDを確認。残した41ケースのプログラムGUIDも前回の記録と一致。[除外ケース](retired-cases.tsv)。
- [ClientSim Udon VM](editor-live-check.txt): 全10596値の型と値が一致。全復元待ちガード、1回だけの書込み、初回復元スナップショット、2ページの表示、NULL／長さ0の配列を明示保存で修復する経路を確認。
- [全件出力と容量表示](editor-export-storage-check.txt): 非同期TSV出力に全92ケースが入り、40配列の長さとint・longの値が一致。容量APIの返り値がUI・TSVへ反映され、管理クラスからの明示再計算要求は入室時と送信後の2回。[生TSV](raw-editor-written.tsv)。
- [クライアント向けUdonコンパイル](client-compile.txt): `IsEditorBuild=false` で成功。C#・Editor向けUdonも成功。ワールドBuild、アップロード、VRChat起動は実施していない。
- [実行中の画面](editor-storage-panel.png): 使用量・上限と送信結果を表示。再入室後の保持検証は実クライアントで行う。
