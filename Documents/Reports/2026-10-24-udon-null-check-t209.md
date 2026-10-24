---
created_at: "2026-10-24T19:38:00+09:00"
updated_at: "2026-10-24T20:04:35+09:00"
category: report
tags: [udon, null-check, performance, reproducibility]
status: completed
experiment_id: UDON-NULL-T209
measured_at: "2026-10-24T19:33:00+09:00"
environment: editor-udon-vm
---

# T209 Udonのnullチェック3方式の比較

今回のUnity 2022.3.22f1／Worlds SDK 3.10.5のEditor Udon VMでは、`if (obj)` は `if (obj != null)` より生成命令が少なく、実測も短時間だった。
ただし、**「暗黙boolはUtilities.IsValidよりも内部処理が短く高速」という一括した主張は支持されなかった。**
暗黙boolとIsValidの判定部分は、ともに2 PUSH＋1 EXTERNの3命令である。
valid入力では両者の差が小さく、型やrunによって順位が変わった。
実nullと破棄済み入力では、IsValidの方が少し短時間という傾向を、Playを入り直した2セッションで確認した。

確認したのは、専用シーン内の局所的な判定と、その結果を加算するループである。
通常C#のベンチマークではなく、実行中のUdonBehaviourのヒープから結果を回収した。
実VRChatクライアント、HMD、Quest、多人数、GC量、ワールド全体のFPSは測定していない。

## 依頼と出典

依頼原文：

> TonboUdonLabsで検証してほしいこと: UdonではGameObjectやアセット、コンポーネント、UdonBehaviour(UnityEngine.Objectに相当するもの) を null チェックするとき、`obj != null` や `Utilities.IsValid(obj)` よりも `obj`(暗黙的な bool 変換) のほうが内部処理が短くなり高速になるという話がある(調べると出典があるかも)。これが実際に正しいのかどうかを検証し、結果をレポートにまとめてほしい。

2026-10-24に `Udon null check implicit bool Utilities.IsValid faster`、`Udon nullチェック 暗黙 bool 高速 IsValid`、`"Udon" "IsValid" "op_Implicit"` などで検索したが、この三者間の順位を直接主張する出典は特定できなかった。
見つかった関連資料と今回の証拠を、以下のように扱う。

| 資料 | 確認内容と証拠の範囲 |
| --- | --- |
| [Unity 2022.3 Object.bool](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Object-operator_Object.html) | 暗黙boolによる存在確認の公式API。Udonの速度は説明しない |
| [Unity公式C#参照実装の2022.3ブランチ](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Runtime/Export/Scripting/UnityEngineObject.bindings.cs) | `implicit bool` と `operator !=` は、ともに `!CompareBaseObjects(...)` を使う。通常Unity側の意味を理解する資料であり、導入済みUnityバイナリやUdon EXTERNの全処理を証明するものではない |
| [UdonSharp公式VRChat API](https://udonsharp.docs.vrchat.com/vrchat-api/) | `Utilities.IsValid(object)` は有効で非nullならtrueを返す。破棄済みGameObjectや退出済みプレイヤーが用途に含まれる |
| [VRChat公式Performance Tips](https://creators.vrchat.com/worlds/udon/udonsharp/performance-tips/) | Udonと通常C#の速度差、処理分散、取得結果の保持を説明する。今回の3方式の順位を示す資料ではない |
| [2020年の破棄済み参照の不具合報告](https://feedback.vrchat.com/udon/p/null-check-on-gameobject-will-throw-exception-if-the-gameobj-is-destroyed) | Editorとクライアントの保護処理の違いに関する過去の報告。2021年の開発者コメントもある。現行クライアントでの再現を今回確認したとは扱わない |
| [2024年の「Utilities.IsValidを使用しよう」](https://zenn.dev/iwacchilab/articles/bb04eb68fac1ac) | 著者自身のSDK 3.7.Xでの比較記事。IsValidを推す別の主張で、暗黙boolが最速という出典ではない。公開コード断片だけから今回と同じ型、入力、反復設計とは判断できない |

今回の性能結論は、上記の記事の数値を転用せず、保存した生成Udonコードと生データから導いた。
SDKのEXTERN内部の全分岐やネイティブ命令数は分解測定していない。
以下でいう「命令が少ない」は、生成Udon Assemblyの範囲を指す。

## 型と入力の意味

比較する各ループは、同じ静的型の2要素配列から `obj = values[i & 1]` を取得し、判定がtrueなら同じ `sum++` を行う。
3方式の比較のために手書きの型castを挟んでいない。
コンパイラが内部で基底型へコピーする処理は、生成コードの差として数えた。

| C#宣言型 | valid入力 | 選定理由 | 3方式のC#／U# compile |
| --- | --- | --- | --- |
| GameObject | 専用のValidUdonInput | シーン内の代表的な参照 | すべて成功 |
| Material | 専用のNullCheckInput.mat | 永続アセットとして作れる代表的なUnityオブジェクト | すべて成功 |
| Transform | ValidUdonInput.transform | 標準コンポーネントの代表 | すべて成功 |
| VRC.Udon.UdonBehaviour | NullCheckTargetのbacking UdonBehaviour | 実行時のUdonコンポーネントそのもの | すべて成功 |
| UnityEngine.Object | ValidUdonInputを基底型として保持 | 宣言型による生成コード差の確認 | すべて成功 |

UdonSharpBehaviourのC# proxy型をUdonBehaviour型の代用にしていない。
NullCheckTargetはEditor側で公式SDKのAddUdonSharpComponentから作成し、backing UdonBehaviourを永続フィールドに設定した。
`object`（System.Object）、VRCPlayerApi、任意のUdonSharp派生型、他のアセット型は今回の測定対象に含めない。
特にSystem.ObjectにはUnityEngine.Objectの暗黙boolをそのまま適用できず、VRCPlayerApiは退出状態もあるため、今回の順位を横展開できない。

| input ID | 入力 | 2回判定したchecksum | 各参照の期待値 |
| --- | --- | ---: | --- |
| 0 | 2要素ともvalid | 2 | true |
| 1 | 2要素とも実null | 0 | false |
| 2 | valid、実nullの交互 | 1 | true、false |
| 3 | 2要素とも破棄済み参照 | 0 | false |

5型×3方式×4入力の60ケースは、両セッションとも期待結果と一致した。
bool対照の4行を加えた各64行を、[意味の検算CSV](2026-10-24-udon-null-check-t209/semantics-editor.csv)と[第1セッションのCSV](2026-10-24-udon-null-check-t209/session-1/semantics-editor.csv)に保存した。

破棄するのは専用GameObject、専用Udon入力のGameObject、専用Rendererから得た実行時Materialコピーだけである。
永続Material、既存シーンのオブジェクトは破棄しない。
Startで参照を保持してDestroyを呼び、120フレーム後に判定する。
生き残ったC#ラッパーを、Exporterで `ReferenceEquals(value, null) == false`、Unityの `value == null` がtrueと確認した。
GameObject、Transform、Material、UdonBehaviourの4参照すべてで成立したため、破棄済み条件を単なる実nullと取り違えていない。
ただしEditorのUdonHeapでの結果であり、実クライアントの保護ヒープを含む同値性の保証ではない。

予備コンパイルでは `new Material(AssetReference)` がUdon非公開APIとして失敗した。
これは判定構文の未対応ではない。
その診断を[予備コンパイル記録](2026-10-24-udon-null-check-t209/pilot-compile-diagnostics.json)に保存し、専用Rendererの `material` getterによる実行時コピーへ修正した。

## 生成された判定処理

[Udon Assembly全文](2026-10-24-udon-null-check-t209/NullCheckBenchmark.uasm)、[判定部分の抜粋](2026-10-24-udon-null-check-t209/predicate-snippets.txt)、[命令集計](2026-10-24-udon-null-check-t209/assembly-summary.csv)を保存した。
数える範囲は、配列getterの直後から判定EXTERNまでである。
配列取得、分岐、加算、ループ制御は数に含めない。

| 宣言型 | != null | Utilities.IsValid | 暗黙bool |
| --- | ---: | ---: | ---: |
| GameObject、Material、Transform、UdonBehaviour | 10命令（7 PUSH、2 COPY、1 EXTERN） | 3命令（2 PUSH、1 EXTERN） | 3命令（2 PUSH、1 EXTERN） |
| UnityEngine.Object | 7命令（5 PUSH、1 COPY、1 EXTERN） | 3命令 | 3命令 |

呼ばれる判定EXTERNは、それぞれ次の1個である。

```text
UnityEngineObject.__op_Inequality__UnityEngineObject_UnityEngineObject__SystemBoolean
VRCSDKBaseUtilities.__IsValid__SystemObject__SystemBoolean
UnityEngineObject.__op_Implicit__UnityEngineObject__SystemBoolean
```

`!= null` は派生型の参照とnullを演算子の引数へコピーする。
UnityEngine.Object宣言では参照側のコピーが不要になる。
暗黙boolとIsValidではそのコピーがなく、生成命令数が一致する。
それぞれ異なるEXTERNを呼ぶため、命令数の一致から内部コストの一致までは導けない。

導入済みU#の `BoundArrayAccessExpression.BuildProperty` は、UnityEngine.Object系配列のgetterを `SystemObjectArray.__Get__SystemInt32__SystemObject` にする。
今回も5型すべてでその命令を確認した。
型を手動でSystem.Objectへ変えて比較したわけではない。

## 計測条件

| 項目 | 条件 |
| --- | --- |
| 基点 | main、HEAD dd1a4ed2fe1f593a328b10d15b73d5e42ed1a337。開始時のGit差分とstagingはなし |
| ソフトウェア | Unity 2022.3.22f1、Worlds/Base SDK 3.10.5、SDK同梱UdonSharp。U# assembly versionは0.0.0.0なので独立した配布版番号とは扱わない |
| VM | VRC.Udon.VM.UdonVM、ヒープVRC.Udon.Common.UdonHeap。ClientSimコンポーネントあり |
| PC | Windows 11 10.0.26200 64bit、i7-9700K、RTX 3070 Ti |
| 電源と描画 | 高パフォーマンス電源プラン、VSync=0、targetFrameRate=72、Game表示1220×687 |
| デバッグ | Profiler=false、Deep Profile=false、UdonDebugLogging=true。個別判定をログ出力しない |
| 時計 | Stopwatch.GetTimestamp、Frequency=10,000,000、IsHighResolution=true。公称tickは100ns |
| N | 128、1024 |
| バッチ | 各構成でwarmup 5周、記録30周、2 run。各caseの記録60サンプル |
| ケース | 時計のみ1、bool配列対照1、5型×3方式の15、計17 |
| 構成 | N 2種×入力4種。各セッション8,160行、Playを入り直した2セッションで16,320行 |
| 順序 | case=(position+phase)%17。run 1は16-caseに反転。phaseはwarmupを含む。乱数seedは使わない |
| 分散 | 1フレーム最大4ケース。第2セッションの最長記録バッチは5.2306ms |
| 正しさ | warmupも含め期待checksumを照合。両セッションでFailures=0、記録の欠落、重複、順序違反なし |

計時は `start=GetTimestamp(); sum=Run(case); ticks=GetTimestamp()-start;` である。
したがってRunの呼び出し、switch、配列取得、判定、分岐、加算、ループ制御を含む。
参照の初期設定、探索、Material生成、Destroy、文字列化、CSV構築、ログ、ファイルI/Oは含まない。
型ごとの3方式は同じ取得方法と加算方法を使う。
validとnullで加算回数が変わるため、方式の比較は同じ型と入力の中で行う。

bool配列対照はループと分岐の大きさを見る参考であり、Unity参照配列の取得コストまで厳密に同じではない。
その差引き値を「null判定だけの純粋なコスト」と呼ぶことはできない。
未差引き値を主結果とし、差引きは同じrun／batchの値との対応を保った参考値にした。
時計対照もRun呼び出しとswitchを含み、第2セッションの中央値は構成別2.0～3.4µs、最小1.7µsだった。
公称tickより実際の計時負荷の方が大きく、100ns差をそのまま検出可能という意味ではない。

測定中はTonboUdonLabsを前面にした。
別Unityプロジェクトや通常のデスクトップアプリは開いたままで、停止やPC設定変更は行っていない。
バックグラウンド負荷、CPU温度、クロック推移は計測していない。

## 実測結果

以下は第2セッションのN=1024、各60サンプルの未差引き値である。
数値はms/batchで、valid表は「中央値／p95」を示す。
全構成のp05、p95、最小、最大、MAD、run別中央値、ns/op、対照差分は[summary.csv](2026-10-24-udon-null-check-t209/summary.csv)にある。

| 宣言型、valid入力 | != null | IsValid | 暗黙bool |
| --- | ---: | ---: | ---: |
| GameObject | 3.6950／4.0688 | 3.1128／3.5018 | 3.0903／3.5021 |
| Material | 2.7185／2.9924 | 2.1944／2.4825 | 2.1839／2.5503 |
| Transform | 4.0403／4.5222 | 3.4292／4.0090 | 3.3942／3.8493 |
| UdonBehaviour | 4.0748／4.3427 | 3.4323／3.7862 | 3.4088／3.9105 |
| UnityEngine.Object | 3.5040／4.0405 | 3.1017／3.5555 | 3.1067／3.4813 |

例えばvalid GameObjectでは、暗黙boolの未差引き中央値は3.018µs/op、IsValidは3.040µs/op、!= nullは3.608µs/opだった。
暗黙boolと!= nullの対応batch差の中央値は-0.591µs/opである。
bool対照は1.42355ms/batchで、暗黙boolの対応batch差引き参考値は1.626µs/opだった。
中央値の差、対応差の中央値、差引き中央値は計算順が違うため同じ値にならない。

次表はnull、混在、破棄済み入力の中央値である。
最後の列は対応batchごとの「暗黙bool−IsValid」を1操作へ換算した中央値で、正なら暗黙boolが長時間だったことを示す。

| 入力 | 型 | != null ms | IsValid ms | 暗黙bool ms | 対応差 ns/op |
| --- | --- | ---: | ---: | ---: | ---: |
| 実null | GameObject | 2.1352 | 1.5540 | 1.6000 | +46.5 |
| 実null | Material | 2.1606 | 1.5643 | 1.5887 | +26.4 |
| 実null | Transform | 2.1416 | 1.5551 | 1.5989 | +39.6 |
| 実null | UdonBehaviour | 2.1393 | 1.5454 | 1.5883 | +42.2 |
| 実null | UnityEngine.Object | 1.9222 | 1.5608 | 1.5915 | +39.5 |
| 混在 | GameObject | 3.0189 | 2.3664 | 2.4339 | +50.9 |
| 混在 | Material | 2.4433 | 1.8868 | 1.9100 | +17.3 |
| 混在 | Transform | 3.1792 | 2.5216 | 2.5398 | +7.2 |
| 混在 | UdonBehaviour | 3.1978 | 2.5281 | 2.5285 | +23.2 |
| 混在 | UnityEngine.Object | 2.8105 | 2.3658 | 2.3969 | +49.5 |
| 破棄済み | GameObject | 2.0912 | 1.5291 | 1.5619 | +21.5 |
| 破棄済み | Material | 2.0615 | 1.4986 | 1.5512 | +24.2 |
| 破棄済み | Transform | 2.1003 | 1.5095 | 1.5456 | +36.4 |
| 破棄済み | UdonBehaviour | 2.0957 | 1.5209 | 1.5860 | +44.3 |
| 破棄済み | UnityEngine.Object | 1.8672 | 1.5129 | 1.5539 | +31.2 |

同じ型の比率を取り、その最小～最大を5型の範囲として示すと次のようになる。
比率は各方式の未差引き中央値の比であり、小さい方が短時間である。

| N | 入力 | 暗黙bool／!= null | 暗黙bool／IsValid |
| --- | --- | ---: | ---: |
| 128 | valid | 0.800～0.899 | 0.980～1.016 |
| 128 | 実null | 0.742～0.832 | 1.017～1.030 |
| 128 | 混在 | 0.781～0.862 | 1.011～1.031 |
| 128 | 破棄済み | 0.735～0.827 | 1.021～1.043 |
| 1024 | valid | 0.803～0.887 | 0.990～1.002 |
| 1024 | 実null | 0.735～0.828 | 1.016～1.030 |
| 1024 | 混在 | 0.782～0.853 | 1.000～1.029 |
| 1024 | 破棄済み | 0.736～0.832 | 1.021～1.043 |

暗黙boolと!= nullの差は、第1セッションのN=1024 validでも比率0.800～0.884となり、同方向だった。
暗黙boolとIsValidは、第1セッションvalidで0.971～1.011と順位が混在し、第2セッションでも普遍的な優劣を識別できない。
一方、実nullと破棄済みではIsValidが短時間という方向が両セッション、両Nに現れた。
第2セッションN=1024では、その対応差の中央値は実nullが+26～+47ns/op、破棄済みが+21～+44ns/opであり、局所的には小さい差である。

[comparisons.csv](2026-10-24-udon-null-check-t209/comparisons.csv)には、型別の比率、対応差、差のp05／p95、対応差中央値のbootstrap区間、run別比率を保存した。
例えばvalid GameObjectの暗黙bool−IsValidは+14.8ns/op、bootstrap 95%区間は-40.4～+51.7ns/opで、符号を特定できない。
valid Materialでは対応差-36.9ns/opの区間が負側にあるが、比率はrun 0で0.975、run 1で1.000となり、同じsession内でも優位の大きさは安定しなかった。
null GameObjectでは+46.5ns/opの区間が+35.4～+60.4ns/opだった。
bootstrapは60個の対応差を2,000回再標本化した記述的な区間で、時間相関や多重比較の補正を行っておらず、全環境での保証には使わない。
外れ値は削除していない。

## 証拠の段階と使い分け

| 段階 | 今回の実施内容 |
| --- | --- |
| 通常C# compile | 成功。最終の新規Editorコードも再コンパイルし、Console error 0を確認 |
| UdonSharp compile | 15判定ケースとStopwatch、実行時入力準備を含め成功。生成AssemblyとSerializedProgramを確認 |
| Editor Udon VM | 2完走セッション、16,320記録行、128意味検算行、checksum成功。追加の診断PlayでVM／ヒープ型を確認 |
| ClientSim | 同梱ClientSimのコンポーネント存在下のEditor測定。ネットワーク動作の比較実験はしていない |
| Build & Test／実VRChat PC client | 未実施 |
| HMD／Quest／多人／GC／長時間 | 未実施 |

この範囲では、**短い生成Udonコードを選ぶ目的なら、暗黙boolとIsValidは両方とも!= nullより有利な候補**になる。
ただしIsValidを暗黙boolへ一律置換する根拠は得られなかった。
UnityEngine.Object系の存在確認として `if (obj)` を読む設計なら、その書き方を使える。
objectやVRCPlayerApiなどを含む有効性チェックを統一したい場合は、公式のIsValidの契約に沿って判断する。
判定結果のbool保持は別の最適化であり、参照の変更や破棄に追従しなくなるため、今回の3方式の代わりに無条件で使えるものではない。

実クライアントで置換を判断するには、同じ入力とバッチ設計をBuild & Testへ持ち込み、最初に破棄済み参照の結果と例外有無を確認する必要がある。
その後にクライアント版、表示モード、ログ設定、実行ヒープを記録して再測定する。
今回のEditorの絶対時間や比率をクライアント値として転記したり、ワールド全体のFPS改善率へ換算したりすることはできない。

## 再実行と保存物

実装は [NullCheckBenchmark.cs](../../Assets/App/World/UdonSharp/NullCheckBenchmark.cs)、[NullCheckTarget.cs](../../Assets/App/World/UdonSharp/NullCheckTarget.cs)、[NullCheckExperiment.cs](../../Assets/App/Editor/NullCheckExperiment.cs)。
シーンは [NullCheckExperiment.unity](../../Assets/App/World/Scenes/NullCheckExperiment.unity)。
新しいProgramAsset本体と.meta、SerializedUdonProgram本体と.meta、Materialと.metaも保持している。
測定原本のCSV 2本とUdon Assemblyは、この実験ディレクトリ内の.gitattributesでGitの改行変換を無効にし、記録したSHA256と原本のバイト列を保持する。

1. Unity 2022.3.22f1でこのプロジェクトを開き、既存の未保存シーンとPrefab Modeを保全する。
2. `Tools > Null Check Experiment > Compile and Export Assembly` でコンパイルする。新規再構築の場合だけ `Create Scene` を使う。既存実験シーンは上書きせず、直接開く。
3. 専用シーンを開く。入力参照、Warmups=5、Batches=30、CasesPerFrame=4を確認し、このシーンだけ保存する。
4. Playへ入る。Startの専用入力破棄後120フレームで自動開始し、最大4ケースずつ進む。`Live Status` の結果は `Library/T209-live-state.txt` に出る。Rowsの増加とFrameの進行を確認する。
5. Complete=True、Rows=8160、Failures=0の後に `Export Editor Result` を実行する。出力はレポート隣のディレクトリ内のraw-editor.csv、semantics-editor.csv、editor-evidence.txtであり、再exportで上書きされる。前の測定を保持したい場合は先に別ディレクトリへ保存する。
6. 必要ならPlay中に `Export Runtime Metadata` を実行し、VM型とヒープ型を取得する。Stopする。実験専用の一時物はPlay終了時に失われる。
7. PowerShell 7から次を実行する。

```powershell
Set-Location 'K:\MagdraTonbo\Projects\VRChat\VRCWorld-TonboUdonLabs'
python Documents/Reports/2026-10-24-udon-null-check-t209/analyze.py
python Documents/Reports/2026-10-24-udon-null-check-t209/analyze.py Documents/Reports/2026-10-24-udon-null-check-t209/session-1
python Documents/Reports/2026-10-24-udon-null-check-t209/analyze_assembly.py
```

解析スクリプトはPython標準ライブラリだけを使い、全構成、順序、反復数、checksum、行の重複と欠落、意味検算をassertする。
Warmups／Batches／N／caseを変更した実験は、別の記録として解析条件も対応させる。
既存の結果と黙って混ぜない。

| 保存物 | 内容 |
| --- | --- |
| [raw-editor.csv](2026-10-24-udon-null-check-t209/raw-editor.csv) | 主表に使った第2セッションの8,160行 |
| [session-1/raw-editor.csv](2026-10-24-udon-null-check-t209/session-1/raw-editor.csv) | 独立した最初のPlayの8,160行 |
| [validation.json](2026-10-24-udon-null-check-t209/validation.json) | 第2セッションの検算、SHA256、周波数 |
| [session-1/validation.json](2026-10-24-udon-null-check-t209/session-1/validation.json) | 第1セッションの検算 |
| [editor-evidence.txt](2026-10-24-udon-null-check-t209/editor-evidence.txt) | 環境、実行時ヒープからの回収、破棄済みラッパーの確認 |
| [runtime-metadata.txt](2026-10-24-udon-null-check-t209/runtime-metadata.txt) | 追加診断PlayのVM、ヒープ、U# assembly、デバッグ状態 |
| [provenance.json](2026-10-24-udon-null-check-t209/provenance.json) | 基点、runtime source、依存と成果物のhash |
| [analyze.py](2026-10-24-udon-null-check-t209/analyze.py) | 検算と集計の再生成 |
| [analyze_assembly.py](2026-10-24-udon-null-check-t209/analyze_assembly.py) | 判定命令数と抜粋の再生成 |

開始時の既存シーンはPlayerObjectPersistenceBatchで未保存変更なし、Play停止中だった。
既存シーンを編集せず、専用シーンを新規作成して測定した。
終了時は専用シーンのEdit Modeに戻した。
U#コンパイル後、既存SerializedUdonPrograms 13件がgit statusで変更表示になった。
Gitの改行正規化を通した実内容の差分はそのうち2件で、残りは内容差を表示しない。
復元や一括削除、差分を隠すindex設定は行っていない。
それらは今回の実験変更に含めるべきものとは扱わず、そのまま残している。
ユーザーの依頼に基づき、実験コード、専用シーン、必要な新規アセット、生データ、解析スクリプト、レポートを1件のローカルコミットの対象とする。
上記の無関係な既存生成物は含めない。
push、upload、他ワールドや共通スキルへの適用は行っていない。
