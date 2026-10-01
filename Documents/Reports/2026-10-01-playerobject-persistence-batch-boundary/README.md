---
created_at: "2026-10-01T10:27:39+09:00"
updated_at: "2026-10-01T12:15:40+09:00"
category: report
tags: [vrchat, udon, persistence, boundary, batch]
status: in-progress
experiment_id: po-persistence-batch-boundary-v2
environment: "Unity 2022.3.22f1 / VRChat SDK Worlds 3.10.5"
---

# short・bool・byteの境界を探す追加ケース

[前回の実クライアント結果](../2026-10-01-playerobject-persistence-batch/README.md)を受け、同じシーンに70ケースを追加した。
シーンは `Assets/App/World/Scenes/PlayerObjectPersistenceBatch.unity`。前回の51ケースと保存用Network ID、公開済みワールドIDを維持し、合計121ケースにする。

## やること

1. 同じシーンを、前回のPrivateワールドへ更新アップロードして入室する。
2. 「一括保存（全ケース）」を1回押し、送信成功121/121を待ってRejoinする。
3. 「復元」を見る。追加ケースは最初に表示され、前／次のページで一覧を切り替えられる。「結果をコピー」は全121ケースを出力する。

初めて追加したケースの保存前の0一致は正常。Rejoin後に再び保存する必要はない。
ページ切替では保存しない。一括保存は表示していないページも含む全ケースが対象。
送信成功はクラウド保存完了の保証ではなく、保持の判定はRejoin後の復元列で行う。

## 追加した個数

| 型 | 新たに追加した個数 |
|---|---|
| short | 1, 16, 24, 26, 27, 28, 30, 32〜49（1個刻み） |
| bool | 28, 32, 40, 48〜60（1個刻み）, 64, 72, 80, 99 |
| byte | 1, 16, 24, 26, 27, 28, 32, 40, 48〜60（1個刻み）, 64, 72, 80, 99 |

それぞれ個数どおりの独立した `[UdonSynced]` スカラーフィールドを持つプログラムを使う。
実行時の比較数だけを変えたり、配列へ置き換えたりしない。
値・seed=101・Manual Sync・VRCPlayerObject／VRCEnablePersistence・一括保存の順序と間隔は前回と同じ。
shortは10101から、boolはすべてTrue、byteは1から。少数ケースも含めて型自体が保存できるか確認する。
候補付近は1個刻み、それ以外は間隔を空けた比較なので、疎な区間で結果が変わった場合は追加の絞込みが必要。

## 実クライアントのRejoin結果（2026-10-01）

ユーザー提供の121ケースのTSVを[生データ](raw-client-restored-20261001-121436.tsv)としてそのまま保存した。
エクスポート日時は2026-10-01T12:14:36.6016506+09:00。保存操作の日時とは別。
クライアント版とプラットフォームは今回のTSVに記録されていない。

| 型 | 保持を確認した最大の個数 | 全値が既定値に戻った最小の個数 | 境界付近の未測定個数 |
|---|---|---|---|
| short | 43（値部分86B） | 44（値部分88B） | なし |
| bool | 64（値部分64B） | 72（値部分72B） | 65〜71 |
| byte | 64（値部分64B） | 72（値部分72B） | 65〜71 |
| int／float | 26（値部分104B） | 27（値部分108B） | なし |
| long／double | 15（値部分120B） | 16（値部分128B） | なし |

今回の条件ではshortの43／44間で結果が変わった。
bool・byteの64保持／72既定値は同じ結果だが、65〜71は置いていないため正確な境界はまだ未確定。
次の絞込み対象はbool・byteの各65〜71個（計14ケース）。
型をまたいだ一律の「固定幅の値部分の合計バイト数上限」では引き続き説明できない。
これらのBは計算上の値部分のサイズで、保存形式のヘッダやフィールド情報、実際の送信サイズを含まない。
保存形式や内部の制限を原因として確定するには、この観測だけでは不十分。

前回の51ケースは、復元・現在一致数と現在値を含むTSVの全列が前回と一致した。
string27フィールド、4096文字のJSONを1フィールドに入れたケース、int[27]を1フィールドに入れたケースは今回も保持された。
全121ケースで復元と現在の一致数は同じ。部分一致はなく、失敗ケースは全値が0またはFalse。
保持されたshort・bool・byteの現在値は、保存用の期待パターンと一致することも確認した。

全行のpost_success／sent_bytesとcloud_commit_ackはunavailable。
今回のTSVはRejoin後の観測で、退出前の書込み・送信成功やクラウド保存完了を独立に証明するものではない。
Editor／ClientSimの全保持という結果とは分けて扱う。

## Editorでの検証

Editor／ClientSimで以下を確認した。合計5604同期フィールド・5630値。

- `identity-preservation.txt`：既存の53個のNetwork IDとワールドIDを保全。前回の51ケースの構成・プログラムGUIDも前回の検証記録と照合して一致。
- `editor-validation.txt`：121個の独立したPlayerObject、同期フィールド数と型、Manual Sync、Persistenceマーカー、UI接続。
- `editor-live-check.txt`／`raw-editor-written.tsv`：実際のClientSim Udon VMで全5630値の型と値を確認。全復元前の書込み阻止、全ケース1回だけの書込み、入室時の復元スナップショット維持、3ページの漏れのない表示と前後切替、切替時の書込みなし、全ケースTSV出力を確認。
- `editor-restoration-check.txt`／`raw-editor-restored.tsv`：ClientSim再入室時に全121ケースの保持を自動表示。自動書込み・送信なし。
- `client-compile.txt`：139スクリプトのクライアント向けUdonコンパイル。Unityのワールドビルド、VRChat起動、アップロードは未実施。
- `editor-panel.png`：追加ケースを最初に表示する画面をEditorカメラで確認。

新規ProgramAssetの生成時に現行ScriptVersionを設定するよう、既存の自動生成ヘルパーも修正した。旧スクリプトの移行は行わない。
実クライアントの追加ケースの結果は上記に記録した。ClientSimの全保持は実クライアントの保存境界を示さない。

再現メニューは `Tools > PlayerObject Persistence > Batch`。
既存シーンへの追加は `Add Boundary Cases`、構成確認は `Validate Scene`。
`Play Fresh Editor Check` → 全ケースの復元を待つ → `Begin Live Editor Check` → 書込み完了後に `Finish Live Editor Check`。
停止後に通常のPlayで入り直し、`Check Editor Restoration` を実行する。
ClientSim保存は本シーンのファイルだけをバックアップし、以前の検証シーンの保存データには触れない。
