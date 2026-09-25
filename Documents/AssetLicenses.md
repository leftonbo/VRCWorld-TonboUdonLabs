---
created_at: "2026-09-25T15:01:00+09:00"
updated_at: "2026-09-25T15:01:00+09:00"
category: licenses
tags: [assets, licenses, fonts, dependencies]
---

# 使用アセット・依存のライセンス台帳

2026-09-25 のファイル・manifest とユーザー申告に基づく初期台帳。以下は各配布物の原文への索引であり、プロジェクト全体に単一のライセンスを付与するものではない。新規導入・配布物更新時に出典と原文を確認する。

| 対象 / 配置 | 出典・権利者 | ライセンス / 原文 | 状態・改変 |
| --- | --- | --- | --- |
| KosugiMaru-Regular.ttf、同名 SDF / Assets/App/ArtResources/Fonts | ユーザー申告: Google Fonts / Motoya | Google Fonts 配布の Apache-2.0、[同梱原文](Licenses/KosugiMaru-LICENSE.txt)、[配布元](https://github.com/google/fonts/tree/main/apache/kosugimaru) | TTF と派生 SDF を使用。SDF の生成設定・TTF 改変歴は未確認 |
| Flat-Panel / Flat-Rounded / Flat-Rounded-Outline.png / Assets/App/ArtResources/UI/Sprites | プロジェクト所有者による自作（2026-09-25 申告） | 外部ライセンスなし。第三者向け利用許諾は未指定 | UI 作成のベース素材 |
| LiberationSans.ttf / Assets/TextMesh Pro/Fonts、関連 SDF | Google / Red Hat | SIL OFL 1.1、[既存原文](../Assets/TextMesh%20Pro/Fonts/LiberationSans%20-%20OFL.txt) | 同梱の著作権・Reserved Font Name 表記を保持 |
| TextMesh Pro resources / Assets/TextMesh Pro、com.unity.textmeshpro | Unity Technologies | Unity Companion License、復元後 Library/PackageCache/com.unity.textmeshpro@3.0.6/LICENSE.md | 導入版 3.0.6。フォント原文は上記の別条件 |
| VRCDefaultWorldScene と付随素材 / Assets/Scenes | SDK サンプル由来と推定 | VRChat SDK の原文を確認（下記） | 個別素材の出典・改変歴は要確認。名前だけで適用条件を確定しない |
| UdonSharp UtilityScripts / Assets/UdonSharp | SDK / UdonSharp 由来 | 復元した com.vrchat.worlds の同梱原文を確認 | 同梱コード・ProgramAsset の原文を保持 |
| com.vrchat.base / worlds 3.10.5 | VRChat | Packages/com.vrchat.base/license.txt、Packages/com.vrchat.worlds/license.txt、[SDK 規約](https://hello.vrchat.com/legal/sdk) | バンドル内の個別 notices も保持 |
| com.vrchat.core.vpm-resolver 0.1.29 | VRChat | Packages/com.vrchat.core.vpm-resolver/License.md（VRChat Distro License） | Dependencies の個別原文も参照 |
| dev.onevr.vrworldtoolkit 3.4.1 | oneVR | MIT、Packages/dev.onevr.vrworldtoolkit/LICENSE、[配布元](https://github.com/oneVR/VRWorldToolkit) | VPM 復元対象 |
| Mulligan Renamer / com.redbluegames.mulligan | Red Blue Games | MIT、復元後 Library/PackageCache/com.redbluegames.mulligan@*/LICENSE.txt | UPM lock の Git hash を正とする |
| Reference Viewer / jp.amagamina.reference-viewer | ina-amagami | MIT、復元後 Library/PackageCache/jp.amagamina.reference-viewer@*/LICENSE.txt | UPM lock の Git hash を正とする |
| Unity Skills / com.besty.unity-skills v2.8.4 | Besty0728 | [配布元](https://github.com/Besty0728/Unity-Skills) の原文要確認 | 導入 package 内に LICENSE ファイルを発見できず。許諾は推測しない |
| その他 Unity UPM / transitive packages | 各配布元 | 各 package の LICENSE / Third Party Notices | 全一覧・版は Packages/packages-lock.json。Unity 製品で条件を一括断定しない |
| .agents/skills の外部スキル | 各配布元 | 各 SKILL.md の license と同梱 LICENSE | エージェント用依存。ワールド素材とは別に原文維持 |

`Library/PackageCache` と復元対象の `Packages` 内パスは clone 直後には存在しない。manifest / lock を使って復元して参照する。外部ライセンス原文は front matter を追記せず、そのまま保存する。新しい配布元が NOTICE を添付している場合はそれも保持する。

## 未確定事項

- 自作コード・UI 素材の外部公開時のライセンスは未指定。今回、任意の MIT 等は付与していない。
- Assets/Scenes のサンプル素材の正確な出典と改変履歴、Unity Skills の適用原文は必要時に確認する。
