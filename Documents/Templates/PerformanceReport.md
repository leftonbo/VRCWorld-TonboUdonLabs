---
created_at: "2026-09-25T15:01:00+09:00"
updated_at: "2026-09-25T15:01:00+09:00"
category: template
tags: [udon, performance, report]
status: planned
experiment_id: null
measured_at: null
---

# 性能検証レポートテンプレート

コピー先は `Documents/Reports/<日付>-<実験ID>.md`。category を report にし、作成・更新日時と ID を設定する。未計測欄は未計測と書き、完了時のみ status を completed にする。

## 問い・仮説

- 比較する1要因、ケース一覧、正しさの期待値:
- 固定条件、対象外:

## 再現情報

- commit / 未コミット差分の保存先:
- Unity / SDK / U# / VRChat / OS / CPU / GPU:
- シーン、コード、実行操作、入力、checksum:
- PC Desktop / VR / Android、人数・アバター、FPS / VSync、電源・負荷:
- 時計 / 分解能 / Frequency、N / 上限、warmup、バッチ数、順番 / seed:
- Profiler / Deep Profile / ログ設定:
- 生データ相対リンク:

## 実施段階

| C# compile | U# compile | Editor / ClientSim | VRChat 実機 |
| --- | --- | --- | --- |
| 未実施 | 未実施 | 未実施 | 未実施 |

## 結果

| ケース | N | サンプル数 | 中央値 ms/batch | p95 | 最小 / 最大 | raw ms/op | 対照差分 ms/op | checksum |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 未計測 | — | — | — | — | — | — | — | — |

## 分析・限界・次の検証

確認した事実、推論、ばらつき、除外データと理由、再現範囲、未確認事項を分けて書く。
