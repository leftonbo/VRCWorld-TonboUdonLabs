---
created_at: "2026-10-01T13:45:31+09:00"
updated_at: "2026-10-01T19:27:00+09:00"
category: report
tags: [vrchat, udon, persistence, boundary, batch, synced-types]
status: completed
experiment_id: po-persistence-all-types-v3
environment: "Unity 2022.3.22f1 / VRChat SDK Worlds 3.10.5"
---

# 型ごとの保存境界を調べる111ケース

シーンは `Assets/App/World/Scenes/PlayerObjectPersistenceBatch.unity`。
[前回の121ケースの実クライアント結果](../2026-10-01-playerobject-persistence-batch-boundary/README.md)を踏まえ、境界から離れた104ケースをシーンから外し、94ケースを追加する。
残す17ケースのプログラム・保存用Network IDとワールドIDを維持する。旧ケースが使っていたIDを新しいケースへ再利用しない。
過去の生データ、ケースのソースとProgramAssetは記録・再現用に残す。
同期対象のスカラー20型をカバーし、既存のJSON4096文字とint[27]の1フィールド対照も維持する。
型一覧と値部分の幅は[公式Networking Specs](https://creators.vrchat.com/worlds/udon/networking/network-details/)を参照した。

## 操作

1. 同じシーンを前回のPrivateワールドへ更新アップロードして入室する。
2. 「一括保存」を1回押し、送信成功111/111を待ってRejoinする。
3. 「結果をコピー」から全111ケースのTSVを送る。ページ切替は表示だけで、保存・コピーは全ページが対象。

初めて追加したケースの保存前の0一致は正常。再入室後に保存を押す必要はない。
送信成功はクラウド保存完了通知ではなく、結果はRejoin後の復元列で判定する。

## スカラーフィールドの個数

| 型 | 配置した個数 |
|---|---|
| bool | 64, 65, 66, 67, 68, 69, 70, 71, 72 |
| byte | 64, 65, 66, 67, 68, 69, 70, 71, 72 |
| int | 26, 27 |
| float | 26, 27 |
| short | 43, 44 |
| long | 15, 16 |
| double | 15, 16 |
| sbyte | 1, 64, 65, 66, 67, 68, 69, 70, 71, 72 |
| ushort | 1, 41, 42, 43, 44, 45 |
| uint | 1, 24, 25, 26, 27, 28 |
| ulong | 1, 13, 14, 15, 16, 17 |
| char | 1, 41, 42, 43, 44, 45 |
| Vector2 | 1, 13, 14, 15, 16, 17 |
| Vector3 | 1, 8, 9, 10, 11, 12, 13 |
| Vector4 | 1, 6, 7, 8, 9, 10, 11 |
| Quaternion | 1, 6, 7, 8, 9, 10, 11 |
| Color | 1, 6, 7, 8, 9, 10, 11 |
| Color32 | 1, 24, 25, 26, 27, 28 |
| VRCUrl | 1, 27, 64, 128 |
| string | 27, 64, 128 |

bool・byteの65〜71が未測定のため、両端64・72を含めた1個刻みの比較にする。
既知の他の型は境界の保持側と既定値側の2ケースを残す。
未調査の型では、同じ幅の型の観測を手がかりにした候補付近と1フィールドの対照を置く。境界を確定したという意味ではない。
候補範囲外で結果が変わる型や可変長型は、実クライアントの結果を受けて追加の絞込みが必要。
stringは従来の9文字パターンを使い、フィールド数64・128を追加する。JSONは別に1フィールド4096文字。
VRCUrlはフィールドごとに異なる `https://example.com/po/101/001` 等のURLを保存する。URLを開く・ダウンロードする処理はない。
URLの生成はEditorで行い、非同期フィールドの配列へ用意する。Udon実行時はそこから同期フィールドへ代入する。
配列の対照は既存のint[27]。配列型ごとの要素数の境界調査は本構成には含まない。

## 値と比較方法

seed=101、Manual Sync、VRCPlayerObject／VRCEnablePersistence、1フレーム1ケースの書込みを維持する。
比較するフィールド数は実際の `[UdonSynced]` 宣言数で固定し、実行時の比較数だけを変えない。
すべての値を既定値と異なる値にする。
sbyteは負数、uintはInt32の範囲を超える正数、charはU+3001からの非ASCII文字を使う。
Vector2〜4は2進数で正確に表せる四分の一刻み。Quaternionは単位長かつ成分が0／1のパターン。
Colorは範囲内の2進数で正確に表せるRGBA、Color32は整数RGBA。
VRCUrlはインスタンス参照ではなく `.Get()` の文字列を比較する。
TSVではcharをU+XXXX、Vector／Quaternion／Colorは丸めない成分列、Color32は整数成分列、VRCUrlはURL文字列として出力する。
値Bは固定幅×個数だけの計算値で、ヘッダ・フィールド情報・圧縮を含まず、実保存サイズを表すものではない。

## 検証記録

Editor・ClientSim・クライアント向けUdonコンパイルは別々に記録する。
2026-10-01 14:26 JSTの[実クライアント結果](client-results-20261001-142619.md)を追記した。
bool・byte・sbyteは64個が全値一致、65個から復元時の一致数が0。
追加した固定長型も境界を隣接個数で確認した。string・VRCUrlは128フィールドまで保持され、上限はまだ未測定。
以下のEditor記録は実クライアント結果とは分けて扱う。

19:24 JSTのRejoin直後TSVも同じレポートへ追記した。
境界は前回と一致し、失敗53ケースは全フィールドが既定値になっていることを値そのものから確認した。

- [構成確認](editor-validation.txt): 111ケースの同期フィールド数・型、Manual Sync、PlayerObject／Persistenceマーカー、IDの重複とUI配線を確認。
- [ID維持の確認](identity-preservation.txt): 残した17ケースの保存IDとプログラムGUID、ワールドIDを維持。追加94ケースには整理前のIDを再利用していない。[除外した104ケース](retired-cases.tsv)を記録。
- [ClientSim書込み確認](editor-live-check.txt): Udon VMの3625値を型と値の両方で比較し、全111ケースが一致。全ケースの復元待ちガード、1回だけの一括書込み、ページ切替が書き込まないこと、初回復元スナップショットと全件TSV出力を確認。[生TSV](raw-editor-written.tsv)。
- [クライアント向けコンパイル](client-compile.txt): `IsEditorBuild=false` で成功。C#・Editor向けUdonもコンパイル成功。ワールドBuild、アップロード、実クライアントは未実施。
- [ClientSim再入室の制限](editor-restoration-check.txt): charが数値トークンとして保存される一方、SDKの復元処理が文字列として読むため例外になり、復元イベントへ到達しない。再入室の全111ケース確認は未完了であり、VRChatクライアントの保存不具合を示す証拠ではない。SDKは変更していない。

再現メニューは `Tools > PlayerObject Persistence > Batch`。
シーンの整理・追加は `Refresh Boundary Matrix`、構成確認は `Validate Scene`。
`Play Fresh Editor Check` → 復元待ち → `Begin Live Editor Check` → 書込み後に `Finish Live Editor Check`。
停止後の通常Playで `Check Editor Restoration` を実行する。ただし現在のSDKでは上記char復元例外により全件確認できない。
