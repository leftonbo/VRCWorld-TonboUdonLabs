---
created_at: "2026-09-25T15:01:00+09:00"
updated_at: "2026-09-25T15:01:00+09:00"
category: procedure
tags: [udon, performance, benchmark, reproducibility]
---

# Udon パフォーマンス検証手順

## 1. 仮説と条件を決める

実験ごとに ID を付け、対象コード・シーン・比較する1要因・期待する同一結果を定義する。実装は `Assets/App/` の必要な役割の下に置く。結果は `Documents/Reports/<日付>-<実験ID>.md`、生データは同じ場所の CSV 等に残す。[レポートテンプレート](Templates/PerformanceReport.md) をコピーして使用する。最初から共通ベンチマーク基盤や大量のシーンを作らず、最小の比較から始める。

| 実験 | 比較条件 | 交絡を避ける確認 |
| --- | --- | --- |
| メソッド呼び出し | inline / 同一 Behaviour の private / public / static helper | 引数・戻り値・計算内容・呼び出し回数を揃える。static が現在の U# でその配置・構文から呼べるか先にコンパイルする。不可なら unsupported と記録する |
| イベント呼び出し | 直接呼び出し / 自分への SendCustomEvent / 別 Behaviour の直接呼び出し / 別 Behaviour への SendCustomEvent | 公開メソッド数・メソッド名・対象参照を固定。ネットワーク送信は別実験 |
| 公開メソッド数 | 同じイベントを、公開メソッド数だけ変えたプログラムで呼ぶ | public/private の直接呼び出しコストと、イベント検索コストを混同しない |
| メソッド内変数 | 同時に使う local の数、スコープ分割、変数再利用、フィールド化 | 宣言しただけの未使用変数で比較しない。演算量・生存期間・初期化回数も記録。変数数の上限を仮定しない |
| 呼び出しと local の関係 | 同じ処理で呼び出しをまたぐ local の数を変える | 必要に応じ生成 Udon Assembly のコピー・ヒープ・命令差を調査。recursive は別条件 |

変数上限の調査では成功する最小ケースから段階的に増やし、失敗した場合は最小再現とコンパイラ出力を保存する。コンパイル上限と実行時間の変化は別の結論にする。実験のための public / static / local の構成を、通常の最適化規約で書き換えない。

## 2. 時計の利用可否と精度を確認する

第一候補は `System.Diagnostics.Stopwatch`。ただし Editor 内の通常 C# で使えることは Udon 公開 API の証明ではない。導入 SDK の **UdonSharp Class Exposure Tree** で constructor / Start / Stop / Reset / ElapsedTicks / Frequency または GetTimestamp 等、実際に使うメンバーを確認し、最小 UdonSharpBehaviour をコンパイルして VRChat 内でも値が増加することを検証する。今回の環境セットアップではこの実行確認は未実施。

- Stopwatch を使う場合は生成・初期化を計測外で行い、ticks の差を `Stopwatch.Frequency` で秒に換算する。TimeSpan の ticks と周波数を混同せず、整数除算を避ける。取得できるなら IsHighResolution と Frequency も残す。
- 未公開・利用不可なら `Time.realtimeSinceStartupAsDouble` を次の候補とし、同様に公開・コンパイル・実機を確認する。使えなければ float の `Time.realtimeSinceStartup` を検討し、起動後の経過時間による精度低下と測定限界を記す。
- `Time.time` / `deltaTime` のようなフレーム単位の値を、同一フレーム中の短い処理時間の計測に使わない。時計の連続読み取りと空ループを測り、ゼロや量子化が多ければ反復回数を増やす。
- Unity Profiler はフレーム全体の CPU / GC / 描画などの切り分けに併用できる。Profiler 接続・Deep Profile の有無を記録し、負荷が異なる測定系列を混ぜない。

## 3. 最小ベンチを実行する

1. 比較ケースの checksum / 戻り値を既知の期待値と照合する。入力は実行時に与え、結果を計測後に保持・確認して処理が無意味な定数化を受けないようにする。
2. 参照取得、配列確保、時計生成、文字列化、ログ、UI 更新は計測区間の外へ出す。同期不要ならネットワーク処理を入れない。
3. ウォームアップを例えば5バッチ行い、回数を記録して集計から除外する。初回コストを知りたい実験は別系列として保存する。
4. 少ない反復回数 N から増やし、タイマー分解能より十分長く、フリーズや Udon 実行制限に達しない短いバッチに調整する。N に上限を設け、実験全体をフレーム間に分散する。各バッチの計測開始・終了は同じフレーム内に置く。
5. 時計だけ、ループ＋同等の結果保持を行う対照、各ケースを同じ N で測る。順番は交互 AB/BA または記録した seed で入れ替える。条件変更を計測内の毎反復 if 文で切り替えない。
6. 各条件30バッチ程度を出発点とし、生の各測定値を保存する。中央値、p95、最小・最大、サンプル数を示す。外れ値は黙って削除せず、除外条件と元データを残す。
7. `raw_ms / N` と `(case_ms - baseline_ms) / N` を併記する。差がゼロ・負・ばらつき以下なら「差を識別できない」とする。丸めで高速化を作らない。速度比の基準と単位を明記する。
8. 同じ条件で複数回の実行を比較し、別の N でも傾向を確認する。必要なら別セッションで再測定する。

生データ例の列: `experiment_id,run_id,case,order,batch,iterations,elapsed_ticks,frequency,elapsed_ms,checksum,environment`。時計が ticks を提供しない場合は ticks / frequency 列を省略する。計測ログの個人情報やローカル絶対パスは共有前に確認する。

## 4. 検証段階と報告

順に C# コンパイル → UdonSharp コンパイル → Editor / ClientSim の機能確認 → VRChat Build & Test の実測を行う。クライアントの実行経路が Udon VM であることを確認する。Editor の proxy C# 実行やテスト用 MonoBehaviour の時間を Udon 性能として報告しない。PC Desktop / PC VR / Android 等は別結果とする。

Unity・SDK・U# の版（SDK 同梱ならその旨）、Git commit と未コミット差分、シーン・ケース設定、VRChat クライアント版、OS、CPU/GPU、電源設定、表示モード、解像度、FPS 制限 / VSync、アバター、人数、バックグラウンド負荷、Profiler とログ設定を記録する。温度やクロック変動が疑わしい場合は再測定条件も残す。

結果は仮説 / 事実 / 推論を分け、再現範囲を明示する。コンパイルだけで性能優劣を結論にしない。動作確認用のログを抑えてから性能を測り、エラー検出自体は省略しない。ネットワーク検証は複数クライアント・ownership・late join 等を別手順として追加する。

## 参考資料

- [VRChat: Performance Tips](https://creators.vrchat.com/worlds/udon/udonsharp/performance-tips/) — public メソッド数とカスタムイベント検索、Behaviour 間呼び出しの注意。速度差は本プロジェクトで測定する。
- [VRChat: Class Exposure Tree](https://creators.vrchat.com/worlds/udon/udonsharp/class-exposure-tree/) — 使用メンバーの公開確認。
- [VRChat: Debugging Udon Projects](https://creators.vrchat.com/worlds/udon/debugging-udon-projects/) — 実機ログとデバッグ設定。デバッグによる性能変化に注意。
