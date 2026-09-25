---
created_at: "2026-09-25T15:01:00+09:00"
updated_at: "2026-09-25T15:01:00+09:00"
category: agent-guidance
tags: [agents, unity, vrchat, udon, performance]
---

# エージェント作業規約

## 目的と配置

- VRChat World SDK / Udon の技術検証・性能検証用プロジェクト。検証で比較する条件を勝手に最適化しない。
- ワールド用の新規実装・素材・シーンは `Assets/App/` 内（本プロジェクトでいう `App/`）。既存の役割別フォルダを利用し、必要になった役割だけ追加する。
- ランタイム namespace は `App`、エディタは `App.AppEditor` をベースとする。エディタコードは `Assets/App/Editor/` または配下の `Editor/` フォルダに置く。通常の C# と Udon 実行コードを混同しない。
- 人向け文書は `Documents/`、調査・分析・検証結果は `Documents/Reports/`。ルート README は目的だけを保ち、実装一覧や進捗を追加しない。

## 作業前と検証

- `git status --short` と対象・呼び出し元を確認し、既存変更・未保存シーンを保全する。Unity / SDK の版は ProjectSettings と Packages の manifest / lock を正とする。
- Windows のシェルは PowerShell 7 (`pwsh`) を使い、作業ディレクトリを明示する。Windows PowerShell 5.1 は互換性上必要な場合のみ。
- 必要な `.agents/skills/` の SKILL.md を読んで使う。一般的な助言より、今回のユーザー指示と測定対象の条件を優先する。
- Unity API / Editor ツールでアセットを操作し、シーンや Prefab の YAML 直接編集は最後の手段とする。アセット移動には `.meta` を伴わせる。
- C# コンパイル、UdonSharp コンパイル、Editor / ClientSim、VRChat 実機を区別して報告する。未実施の段階を成功扱いしない。
- 性能検証は必ず [PerformanceTesting.md](Documents/PerformanceTesting.md) を読み、環境・生データ・再現手順を残す。未計測の速度比を事実として書かない。

## Git / GUID

- [GitWorkflow.md](Documents/GitWorkflow.md) に従う。Assets と `.meta`、ProjectSettings、Packages の manifest / lock、規約・スキルを共有する。
- `Assets/SerializedUdonPrograms/`（ユーザー表記 SerializeUdonPrograms）および U# 付随の ProgramAsset `.asset` は生成物だが、参照維持のため**作成時に本体と `.meta` を必ず追加**する。
- 既存生成物のコンパイルだけによる変更は通常コミットしない。参照・設定変更が必要なら理由を説明して対象を限定する。生成物を一律削除・ignore したり、`.asset` 全体を生成物と見なしたりしない。
- `assume-unchanged` / `skip-worktree` で差分を隠さない。確認なしの restore / reset や一括 stage をしない。
- LFS の対象と導入確認は GitWorkflow を参照。新しい外部素材は [AssetLicenses.md](Documents/AssetLicenses.md) に出典・ライセンス・改変を記載し、原文を保持する。

## Markdown メタデータ

プロジェクトで新規作成・実質更新する `.md` は先頭に YAML front matter を付ける。

```yaml
---
created_at: "2026-09-25T15:01:00+09:00"
updated_at: "2026-09-25T15:01:00+09:00"
category: report
tags: [udon, performance]
---
```

- 日時はタイムゾーン付き ISO 8601。`created_at` は保持し、内容更新時に `updated_at` を更新する。タグは2個以上の文字列配列。
- category は `overview`, `agent-guidance`, `workflow`, `procedure`, `report`, `template`, `licenses` を優先し、必要なら追加する。タグは小文字 kebab-case を優先する。
- レポートには `status` (`planned`, `in-progress`, `completed`, `inconclusive`)、`experiment_id`、`measured_at`、`environment` 等を必要に応じて追加できる。作成日時と計測日時は別物。
- 外部由来のライセンス原文・SDK・既存配布スキルの文書は原文を保つため一括改変しない。必要な分類情報はプロジェクトの台帳に記載する。
