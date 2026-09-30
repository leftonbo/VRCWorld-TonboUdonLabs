---
created_at: "2026-09-30T21:24:44+09:00"
updated_at: "2026-09-30T21:40:00+09:00"
category: report
tags: [vrchat, udon, persistence, player-object, rejoin]
status: completed
client_reproduction: not-run
experiment_id: po-persistence-schema-v1
environment: "Unity 2022.3.22f1 / VRChat Worlds and Base SDK 3.10.5 / bundled UdonSharp / Windows Editor and ClientSim"
---

# PlayerObjectの変数追加後に保存データが消える現象を切り分ける

専用シーン `Assets/App/World/Scenes/PlayerObjectPersistenceExperiment.unity` に、状態の読取り・書込み・比較・TSV出力を行うワールド配置UIを追加した。
最初に19変数で保存とRejoinを確認し、同じワールド・同じオブジェクトを27変数に更新して、**書込み前の復元値**を採取する。
変数数だけの問題か、保存済みデータに変数を追加したときの問題かを、この順序で切り分ける。
今回の完了範囲はEditor実装とEditor / ClientSimでの確認まで。実機での再現・原因確定、Build & Test、アップロードは含めない。

## 19→27の報告を起点に、変数数とスキーマ変更を分ける

本人の報告は「int19個に8個追加した際、保存完了後のRejoinで対応するPlayerObjectのデータが消えた。内部エラーは見つからず、ClientSimでは再現しない」というもの。
19と27の間に上限があることや、保存処理の特定箇所が原因であることは未確認である。
この実装では保存対象の変数数を `[UdonSynced] int` の数として扱う。元の19個に非同期の変数が含まれていた場合は、元コードの属性と配置も照合する必要がある。

UIから選ぶ3ケースは、それぞれ独立したPlayerObjectテンプレートを持つ。
各テンプレートの同一GameObject上に、`VRCPlayerObject`、`VRCEnablePersistence`、Manual同期のUdonBehaviourを配置した。

| case | UIの選択 | 保存するフィールド | intの数 | 用途 | Network ID |
| --- | --- | --- | ---: | --- | ---: |
| 0 | 変更対象 scalar | `V001`〜`V019`、後から末尾へ追加 | 初期19、変更可能 | 同じ保存データに19→27変数を追加する | 12 |
| 1 | 固定27 scalar | `V001`〜`V027` | 27 | 最初から27変数あるケースと比較する | 11 |
| 2 | int[27] 対照 | `Values`という同期配列1フィールド | 27要素 | intの数を揃え、同期フィールド数を減らす | 10 |

配列とscalarではシリアライズ形式も違うため、同じ保存バイト数の比較にはならない。
使用量は3ケースを含むワールド全体のPlayerObject使用量であり、単一ケースの使用量ではない。
固定27だけが残る、変更対象だけが消える、といった差から次の実験を選ぶ。

共通の `PersistenceProbe` は観測と手動書込みを担当し、同期フィールドを持たない。
ケース名・観測カウンター・UI参照も同期しないため、観測用の保存変数を19個へ上乗せしていない。
シーンからテンプレートへの参照で書き込まず、復元済みのローカル所有インスタンスが登録した参照を使用する。

## 復元値を変えずに、保存と同期の各段階を観察する

起動時やRejoin時の自動書込みは行わない。
`Start`の値は復元前の参考記録として残し、保存済みデータの判定には使わない。
`OnPlayerRestored`で対象所有者を確認してから書込みを許可し、nullを0へ自動補正しない。
公式仕様でも、保存データの利用は復元イベント後に行う。[PlayerObject仕様](https://creators.vrchat.com/worlds/udon/persistence/player-object/)

`RequestSerialization`、`OnPreSerialization`、`OnPostSerialization`を個別に記録する。
`OnPostSerialization.success`は送信試行の結果であり、このUIではクラウド保存完了の通知として表示しない。
`OnDeserialization`の`isFromStorage`も採取する。これも現在の復元元の情報であり、直前の書込みに対するサーバー保存完了通知ではない。[Networkingイベント仕様](https://creators.vrchat.com/worlds/udon/networking/network-components/)

使用量取得ボタンは `Networking.RequestStorageUsageUpdate()` を呼び、通知後に使用量と上限を読む。
未取得の値と時刻は `-1`。手動要求は10秒間隔に制限した。
導入SDK 3.10.5の `UdonSharpBehaviour.OnPersistenceUsageUpdated()` は引数なしであり、Web上のイベント表の引数表記とは異なる。
実装は導入SDKのソースとコンパイル結果を正としている。

公式の容量制限は、1プレイヤー・1ワールドあたりPlayerObject用100KB。PlayerDataの100KBとは別枠で、サーバーでは圧縮される。
この仕様から「27個のintが変数数上限」とは結論できない。
同一アカウントで同じワールドを複数クライアントから同時に利用するとデータの上書きが起こり得るため、初回の比較は1クライアントで行う。[Persistence仕様とテスト環境](https://creators.vrchat.com/worlds/udon/persistence/)

類似する公開報告として、保存済みPlayerObjectに同期変数を追加すると、新規変数がnullとなり、同期・更新が止まるケースがある。
2026-09-30に確認したページの表示は `available in future release` で、2026年のコメントにも再発報告がある。
今回の現象と同じ原因か、利用するクライアント版で修正済みかは未確認である。
この情報を踏まえ、生のnullを記録する操作と、nullのみを0へ初期化する診断操作を分けた。[変数追加時の既知報告](https://feedback.vrchat.com/persistence/p/adding-new-udonsynced-fields-to-a-player-object-appears-to-make-saved-data-read)

## 初回は19変数の保存とRejoinを確認する

1. 専用シーンを開く。納品時の変更対象は19変数。既存ワールドのシーンは変更していない。
2. `Tools > PlayerObject Persistence > Validate Scene`を実行し、3ケースの配置と同期変数数を確認する。
3. 本人が新しいPrivateワールドとしてアップロードする。このシーンのBlueprint IDは空にしてある。既存の検証ワールドを上書きしないよう、新規ワールドとして扱う。
4. 実機で新しいインスタンスへ入る。使用したWorld ID、ワールド版、VRChatクライアント版、PC Desktop / PC VR / Android、人数、アカウントのデータ履歴を外部メモに残す。
5. UIが「復元済み・書込み可」になったら、まだ書き込まずに「TSV出力」。Desktopでは出力欄をクリックし、Ctrl+A → Ctrl+Cでファイルへ貼り付ける。
6. `run / 条件メモ`を `baseline19-write1`、seedを `101`、比較数を `19` にする。「変更対象 scalar」→「全int書込＋送信」を1回押す。
7. 値は `seed * 100 + 1始まりの番号`。seed=101なら1番は10101、19番は10119となる。「seedと比較」で19/19一致を確認する。
8. `request`→`pre`→`post`、`success`、`bytes`を確認し、「使用量取得」も押してTSVを保存する。保存完了と判断したVRChat側の表示がある場合は、表示内容と時刻も外部メモへ記録する。
9. 例えば送信後60秒待つ条件を最初の試行として固定し、実際の待ち時間を記録してからRejoinする。60秒は保存保証の時間ではない。短い待ち時間・長い待ち時間は別試行で比較する。
10. Rejoin後は書込み前にTSVを保存する。同じseed=101、比較数=19で比較し、値が残っていることを確認する。
11. 固定27と配列も同じseedで保存・Rejoinを行う。それぞれ比較数を27にする。操作順は初回と逆順の試行も残す。

ログはセッションを越えて保持しないため、**退出前とRejoin直後のTSVを別ファイルに保存する**。
全クライアント終了、別インスタンスへの参加、別端末からの参加は、同じインスタンスへのRejoinとは別の条件として扱う。
データ消失を見つけたときは、書込みや初期化をせずにログを保存する。

## 同じワールドを27変数に更新し、先に追加フィールドを読む

1. 19変数で保存した実機データとTSVを残し、Edit Modeで専用シーンを保存する。
2. `Tools > PlayerObject Persistence > Schema > 27 ints (19 plus 8)`を選ぶ。`PersistenceScalars.cs`の指定領域だけを更新し、`V020`〜`V027`を末尾へ追加する。
3. C#リロードを待つ。Unityのコンパイルでシーンがdirtyになった場合は、その専用シーンを保存する。
4. `Compile and Finish Setup`を実行し、`Validate Scene`の記録で変更対象が `intCount=27 compiledSyncedFields=27` になったことを確認する。更新前とNetwork ID、script GUID、ProgramAsset GUIDを比較する。
5. 本人が**同じWorld ID**へ更新版をアップロードする。シーンやPlayerObjectを再作成しない。Network IDの再生成も行わない。
6. 更新後の新しいインスタンスへ入る。まだ19変数版を使う既存インスタンスとの混在は、最初の試験から除く。
7. 書込み前に「変更対象 scalar」→「読取り」→「TSV出力」。`OnPlayerRestored`時点で旧19個が残るか、新8個が0かnullか、全体が既定値へ戻っているかを確認する。
8. seedを以前と同じ101、比較数を19にして「seedと比較」。追加した8個は、旧データの19個と分けて読む。
9. 生ログを保存した後、「値を変えず送信」を別試行として実行する。新規変数がnullの場合も補正せず、送信結果を記録する。
10. 次に「先頭N書込＋送信」を比較数19で行う試行を用意する。追加した8個を変更せず、旧変数だけを更新する際の結果を調べる。
11. 別の試行では「全int書込＋送信」で27個全てへ値を入れる。新しいseedを使い、比較数27で送信後とRejoin後を照合する。
12. nullを観察した試行の記録後に限り、「診断: NULLのみ0初期化＋送信」を試せる。これは対処によって結果が変わるかを調べる操作であり、原因の確定や通常の自動修復ではない。

変更対象は19・20・23・26・27・28・32変数へ変更できる。
19→20でも失敗するなら、27個という境界だけを探すより変数追加互換性を優先して調べる。
26→27付近だけで失敗する場合は、その近傍を繰り返す。
減らす操作は同じ保存履歴の巻戻しとして解釈しない。各開始スキーマの新規データは、別の試験用アカウントや新規検証ワールドで用意し、履歴を記録する。
この実装にはUser DataやClientSimStorageを削除する操作を追加していない。

## TSVから、復元・送信・上書きのどこで差が出たかを読む

出力は `utc, elapsed_s, build, run, event, case, detail` の7列をタブで区切ったもの。
`detail`内はセミコロン区切りで、`values`の番号と値は縦棒区切り。列名の並びは出力先頭に含まれる。
`build`にはスキーマと実行環境を入れ、Editorの記録は `Editor-ClientSim`、クライアントは `VRChat-client` と表示する。
`run`はイベント発生時のメモであり、入力を変えても過去の行は変更しない。

| 記録 | 読み方 |
| --- | --- |
| `start-before-restored` | 復元前の参考値。消失判定に使わない |
| `player-restored` | 復元イベント直後の生値。書込み前の比較で最初に読む |
| `NULL`、`nulls`、`shape` | intへキャストする前の観測。配列自体のnull、配列長も区別する |
| `request / pre / post` | 要求回数・送信前イベント回数・送信結果イベント回数。要求だけでは送信完了を意味しない |
| `success / bytes` | 送信試行の結果。クラウドへの保存完了は未通知 |
| `deserialization / fromStorage` | ストレージ由来か、他クライアント由来か。イベントが来なかった場合も無理に推定しない |
| `owner / localOwner / ready` | 所有者と復元状態。ローカル所有かつ復元済みの場合だけ書込む |
| `networkId / build / ints / syncedFields` | 変更前後の対象とスキーマを照合する。GUIDはEditor側の検証ファイルに記録する |
| `usage / limit / usageTime` | ワールド全体のPlayerObject使用量と上限、最後に通知を受けた時刻。未取得は-1 |
| `check-expected / matched` | 指定seedの先頭N個の値が一致した数。これ単独で保存場所や消失原因を断定しない |
| `storage-warning / storage-exceeded` | ローカルプレイヤーの容量警告・超過イベント |
| `dropped_rows` | 64000文字を超えて記録できなかった行数。0でない試行は完全なログとして扱わない |

UIはローカル本人のデータを操作する。ログにはリモートPlayerObjectの復元・受信イベントも記録するが、リモートの値を書き換える操作は持たない。
協力者との同期を調べる際は、各自のTSVでownerと値を照合する。
同じアカウントの複数起動による比較は、保存競合と混ざるため別条件にする。
クライアントログには `[PersistenceLab]` でも同じ行を出力する。Udonの停止や別のエラーはVRChatのログも併せて確認する。

| 観察結果 | 次に切り分ける条件 |
| --- | --- |
| 固定27・配列は残り、変更対象だけが変数追加後に失敗 | 保存済みスキーマとの互換性、新規フィールドのnull、旧19個だけ更新した場合 |
| 新規データの固定27でも失敗 | 変数数・scalar形式、送信結果、使用量、待機時間 |
| post成功でもRejoin後に以前の値へ戻る | 送信とサーバー保存の違い、退出タイミング、同一アカウントの別クライアント、利用ワールド版 |
| requestだけ増え、preまたはpostが来ない | 同期要求の処理、ネットワーク状態、Udon停止、復元・所有者状態 |
| Network IDや対象GUIDが変わった | 対象の識別が変わった交絡。変数数上限の証拠から外す |
| null初期化後だけ送信・再復元が改善 | nullとの関連を示す候補。実機で繰り返し、対照と比較する |

必要ならWorld Debug ViewsのLog ViewerやNet Objectsで、Udonエラー・対象所有者・送信状態も照合する。[公式のDebug Views](https://creators.vrchat.com/worlds/udon/world-debug-views/)
元ワールド固有の要因が疑われる段階では、複数Behaviour、子階層、同期モード、初期値、変数型、Network ID、他の自動書込みを一つずつ合わせる。
初回から全条件を混ぜると、最小の再現条件が分からなくなるため、このシーンでは3ケースと手動操作を出発点とした。

## Editorでの証拠と実機で未確認の項目を分ける

環境の正は `ProjectSettings/ProjectVersion.txt`、`Packages/vpm-manifest.json`、導入済みSDK package.json。Unity 2022.3.22f1、Worlds / Base SDK 3.10.5、SDK同梱UdonSharpで確認した。
既存のSharedDataExperimentとUdonPerformanceLabの実装・シーン、開始時から変更されていた既存SerializedUdonProgramsは保全した。
共通のベンチマーク基盤や外部依存は追加していない。

- `editor-validation.txt`: テンプレート3個、Persistenceマーカー、Manual同期、実際にコンパイルされた同期フィールド数、Network ID、11ボタンのUdonイベント接続、出力欄を検査する。
- `editor-live-checks.txt`: ClientSimで生成されたインスタンスのUdon VMから値を取得し、復元・所有者、復元前の送信拒否、不正seedの拒否、ボタン経由の書込み、全値と期待値の比較、出力内容を検査した。合成したnullの読取り、比較、先頭N書込みによるnull保持、明示的初期化、使用量取得も合格した。nullの注入はEditor内の検査であり、報告された実機の現象を再現した証拠にはしない。
- `raw-editor.tsv`: 最終ライブ検査セッションのUI出力から取得した生ログ。実機のデータとして集計しない。
- `editor-restored-19.txt / raw-editor-restored-19.tsv`: Play Mode再起動後、書込み要求0のまま以前の値を読んだ検査。
- `editor-restored-27.txt / raw-editor-restored-27.tsv`: 19→27更新後、書込み要求0のまま旧19個が残り、新8個は0だった記録。ClientSim内の観測に限定する。
- `schema-roundtrip.txt`: 19→27→19の変更で同期変数数と識別子を比較する記録。
- `client-udon-compilation.txt`: `IsEditorBuild=false` のUdonSharpコンパイルも合格。Unity Playerのビルドや実クライアントの起動は行っていない。
- `editor-panel.png`: Editor内で描画したワールドUI。VRChat内やHMDでの見え方の証拠にはしない。

導入ClientSimの `ClientSimUdonEncodeDecode.PostEncode` は `data.Count * 4` を `byteCount` とし、successをtrueにして通知する。
そのため配列1フィールドは27要素でも4 bytesと表示される。この値を実機の同期サイズや圧縮後のクラウド保存サイズへ換算しない。
ClientSimの使用量取得にも「Player Object usage is not accurate in ClientSim」というSDK側の警告がある。Editorの使用量を実機の保存容量として扱わない。
同じソースのDecodeは保存データに存在するキーを復元し、存在しない新規フィールドの扱いは実クライアントと一致する保証がない。

再確認は `Compile and Finish Setup` → `Validate Scene`、Play Mode開始 → 復元待ち → `Run Live Editor Checks` で行う。
ライブ検査はこの検証シーンのClientSimデータへseed=701を書き込む。既存ローカルデータを削除しない。
`Check Restored Editor Pattern`は、その値が次のPlay Modeで残るかを書込みなしで検査する開発用メニュー。
これらの合格から、本人の報告した現象が実機で再現しない、修正できた、原因が確定したとは扱わない。

実機調査の最初の到達点は、19変数での保存・再復元と、27変数へ更新した後の書込み前の生値を揃えること。
この2つの記録と固定27・配列の対照を比べてから、変数数の境界、null、退出時間、元ワールド固有の条件のどれを先に追うか決める。

Editor実装の受入れは完了。Privateアップロード、SDKのビルド時検証、実機のUI入力・コピー操作、実機での消失再現、複数プレイヤー・端末比較は本人の次の作業となる。
納品状態は変更対象19変数、Fixed27は27変数、配列は27要素。ローカルPersistenceデータを削除せず、Git登録もしていない。

コミット前のC#・Markdown・検証用テキストの `git diff --cached --check` は合格した。
全ファイルを対象にすると、Unityが生成した `.asset / .meta / .unity` の末尾空白と、TSVの空の最終列に対して警告が出る。
生成形式や生ログの列構造を保つため、これらを手作業で整形していない。新規Assetsの `.meta`、GUIDの重複なし、画像のLFS属性、ローカルデータのGit除外も確認した。
