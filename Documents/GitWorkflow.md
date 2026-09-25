---
created_at: "2026-09-25T15:01:00+09:00"
updated_at: "2026-09-25T15:01:00+09:00"
category: workflow
tags: [git, lfs, unity, guid]
---

# Git と環境復元

## 新しい PC / clone 後

1. Git と Git LFS をインストールし、リポジトリ内で `git lfs install --local`、`git lfs pull` を実行する。clone 前の `git lfs install` はユーザー全体へ設定する選択肢。
2. VRChat Creator Companion にプロジェクトを登録する。`Packages/vpm-manifest.json` の依存を復元する。VRWorld Toolkit のリポジトリが未登録なら [公式配布元](https://github.com/oneVR/VRWorldToolkit) を確認して登録する。
3. `ProjectSettings/ProjectVersion.txt` の Unity で開き、UPM の復元を待つ。`Packages/manifest.json`・`packages-lock.json`・`vpm-manifest.json` は必ず共有する。
4. C# / UdonSharp コンパイルとシーン参照を確認する。clone 直後の GUID 再生成や意図しない大量差分を正常扱いしない。

セットアップ時の版は Unity 2022.3.22f1 / VRChat SDK 3.10.5。以後は manifest / lock を確認する。VPM の復元対象4ディレクトリだけを ignore し、独自 embedded package は自動除外しない。Library / Temp / UserSettings / IDE 生成物は共有しない。

## アセット追加・変更

- `git status --short --untracked-files=all` で確認後、対象パスを明示して `git add -- <path>`。新規アセットとその `.meta`、新規親フォルダの `.meta` を揃える。
- UdonSharp の `.cs` と生成 ProgramAsset `.asset` / `.meta`、`SerializedUdonPrograms` の新規 `.asset` / `.meta` は作成時に追加する。シーンが参照する GUID の初期共有が必要。
- 既存生成アセットの再コンパイル差分は通常 stage しない。`.meta` の GUID 変更は必ず調査する。既存生成物に必要な参照・設定変更がある場合だけ、その理由を添えて含める。
- Git ignore は追跡済みファイルの更新を止められない。「作成時のみ」はレビュー運用とし、差分を隠す index フラグや自動破棄フックを使わない。
- シーン・Prefab・マテリアル・SDF フォントなどの `.asset` は一括除外しない。Visible Meta Files と Force Text は既に設定済み。
- `.gitattributes` は画像・モデル・音声・フォントなどを LFS に振り分ける。Unity YAML と `.meta` は通常 Git。バイナリ `.asset` が追加されたらファイル単位で LFS 属性を設定する。
- 拡張子を追加する場合は `git check-attr filter diff merge -- <path>` で確認する。既存追跡ファイルの LFS 変換や履歴移行は別作業として扱う。

## コミット前

```powershell
git status --short
git diff --cached --stat
git diff --cached --check
git lfs status
git lfs ls-files
```

ステージ内容、GUID、生成物、秘密情報、ライセンス原文を確認する。リモートは LFS 対応・容量を確認してから設定する。LFS pre-push フックは clone ごとに `git lfs install --local` で導入する。初期セットアップではローカル Git / LFS のみ初期化し、初回コミット・リモート設定・push は行っていない。
