---
name: git-commit-message
description: Format commits for this repository with gitmoji and Japanese summaries. Use when the user explicitly asks Codex to commit, prepare a commit message, or review commit granularity; this skill never grants permission to commit.
---
# Git commit message rules

Use this skill when the user requests a commit message, a commit-granularity
review, or an actual commit. Follow AGENTS.md for authorization.

Message drafting and granularity review do not require authorization to commit.
Run `git commit` only when the user explicitly requests it. Commit authorization
does not authorize push, merge, or PR creation.

When committing, include only the authorized changes and preserve unrelated
staged and unstaged work.

Write commit messages in Japanese. Use the following format:

```text
<intention> [scope?][:?] <message>
```

- intention: Emoji chosen from the gitmoji list below.
- scope: Optional string indicating the change area.
- message: Summary of the change.

## gitmoji list

- 🎉: Project bootstrap (first commit)
- ✨️: New feature
- 🐛: Bug fix
- 🩹: Minor bug fix
- ♻️: Refactoring
- ⚡️: Performance improvement
- 🔥: Remove unused code or files
- 🔨: Editor tooling changes
- 🔧: Parameter or config file changes
- 💡: Add or update comments
- 📝: Add or update documentation
- ✏️: Typo fix
- 🔊: Add or update logging
- 🔇: Remove logging
- 🙈: gitignore changes
- 🚚: Move or rename files or folders
- 🗃️: Database changes
- 🍱: Asset changes (images, audio, etc.)
- ➕️: Add dependency
- ➖️: Remove dependency
- ⬆️: Upgrade dependency
- ⬇️: Downgrade dependency
- ✅️: Add, update, or fix tests
- 🚧: Work in progress
- ⚗️: Experimental code
- 🔀: Merge commit
- ⏪️: Revert

## Examples

```text
📝 agents: 作業範囲と検証条件を明確化
```

```text
🐛 medal-spawn: 手動投入クールダウンがリセットされない不具合を修正
```

## Detailed messages

When details help explain a commit, separate the subject and body with a blank
line, then describe the changes in the body.

```text
📝 agents: 作業範囲と検証条件を明確化

- AGENTS.mdとプロジェクトSkillsの承認条件を統一
- 調査と実装の適用範囲を明記
```

## Notes

- **Commit granularity**: Split commits by purpose; one commit, one goal.
- **Clarity**: Describe changes concretely so the intent is obvious at a glance.
- **Consistency**: Match the style of existing commit messages in the repository.
