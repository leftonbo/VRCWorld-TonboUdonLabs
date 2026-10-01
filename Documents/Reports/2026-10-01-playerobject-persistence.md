---
created_at: "2026-10-01T22:44:19+09:00"
updated_at: "2026-10-01T22:44:19+09:00"
category: report
tags: [vrchat, udon, persistence, player-object, rejoin]
status: completed
experiment_id: po-persistence-showcase-v5
measured_at: "2026-10-01T21:21:52.9551400+09:00"
environment: "Unity 2022.3.22f1 / VRChat SDK 3.10.5; user-provided VRChat client export, client version and platform unrecorded"
---

# PlayerObject Persistence検証の現在の構成と結果

現在の検証は、`Assets/App/World/Scenes/PlayerObjectPersistenceBatch.unity`の200ケースを対象とする。
固定長18型を値幅順に並べ、各型に1フィールドの対照と、最大保持数の前後3個を置いた。
string・VRCUrl・JSONとint[]・long[]は後半に配置する。全11ページ、13,191値を比較する。

| 読みたい情報 | 資料 |
|---|---|
| 操作手順・ケース構成・Editor確認 | [現在の200ケース構成](2026-10-01-playerobject-persistence-showcase/README.md) |
| 最新の実クライアント結果・条件・限界 | [2026-10-01 21:21 JSTの結果](2026-10-01-playerobject-persistence-showcase/client-results-20261001-212152.md) |
| 最新結果の生データ | [Rejoin後の200ケースTSV](2026-10-01-playerobject-persistence-showcase/raw-client-restored-20261001-212152.tsv) |

## 最新の実クライアント結果

200ケースのうち146ケースが全値保持、54ケースが全値既定値だった。部分一致はない。
固定長144ケースは90ケース保持、54ケース全値既定値で、全18型の1フィールド対照が保持された。

| 値幅 | 型 | 保持された個数 | 全値既定値だった個数 |
|---|---|---|---|
| 1 B | bool, byte, sbyte | 1, 61～64 | 65～67 |
| 2 B | short, ushort, char | 1, 40～43 | 44～46 |
| 4 B | int, uint, float, Color32 | 1, 23～26 | 27～29 |
| 8 B | long, ulong, double, Vector2 | 1, 12～15 | 16～18 |
| 12 B | Vector3 | 1, 7～10 | 11～13 |
| 16 B | Vector4, Quaternion, Color | 1, 5～8 | 9～11 |

値幅は値部分の計算上の幅であり、送信量やクラウド保存量ではない。
`最大保持数 = ceil(128 / (値幅 + 1))`は今回の固定長ケースに一致する観測上の式で、内部形式や原因は未確認。

string・VRCUrlは最大512フィールド、JSONは1フィールド4096文字、int[]・long[]は最大1024要素まで、配置した全56ケースが保持された。
この範囲で上限は見つかっていない。配列は各ケース1同期フィールドで、配列フィールド数の上限は調べていない。

使用量APIはPlayerObject全体で64590 / 102400 B、更新待ちFalseを記録した。
ケースごとの保存量ではなく、APIの更新や送信成功はクラウド保存完了通知でもない。
ClientSimの使用量とEditorの保持結果は、実クライアントの測定値と分けて扱う。
クライアント版・プラットフォーム・人数・退出前の待機時間は添付TSVに記録されておらず、内部原因や他の値・混在構成での保持は未確認。

## 過去の構成と結果はArchivedに保存する

以下は各段階の構成・仮説・観測を残す履歴資料である。
初期セットアップ用のエディタ拡張は削除済みのため、過去資料にあるメニュー操作は現在の手順として使わない。
各段階の生TSV、Editor記録、画像、照合スクリプトを同じフォルダに保存している。

| 段階 | 過去資料 |
|---|---|
| 19→27変数追加の初期切り分け | [初期レポート](../Archived/2026-09-30-playerobject-persistence.md) |
| 型ごとの26・27フィールド比較 | [型切替検証](../Archived/2026-10-01-playerobject-persistence-types/README.md) |
| 51ケースの初回一括検証 | [初回一括検証](../Archived/2026-10-01-playerobject-persistence-batch/README.md) |
| short・bool・byteを追加した121ケース | [境界の絞込み](../Archived/2026-10-01-playerobject-persistence-batch-boundary/README.md) |
| 111ケースの型別境界とRejoin直後の観測 | [全型検証結果](../Archived/2026-10-01-playerobject-persistence-all-types/client-results-20261001-142619.md) |
| string・VRCUrl・配列と使用量を調べた92ケース | [可変長・配列の検証結果](../Archived/2026-10-01-playerobject-persistence-variable-array/client-results-20261001-201041.md) |
