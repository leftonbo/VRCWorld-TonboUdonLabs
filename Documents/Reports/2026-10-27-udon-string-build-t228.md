---
created_at: "2026-10-27T13:30:00+09:00"
updated_at: "2026-10-27T20:57:27+09:00"
category: report
tags: [udon, performance, string-builder, benchmark]
status: completed
experiment_id: UDON-STRING-T228
measured_at: "2026-10-27T13:17:00+09:00"
environment: [Editor-UdonVM-ClientSim, VRChat-PC-Desktop]
---

# Udonの文字列連結とStringBuilderを実測比較する

**PC Desktopの実クライアントでは、短い8断片は通常連結が速く、32・128行の統計表示ではStringBuilderが有利**だった。
数値128行は通常連結が中央値378 µs、StringBuilder再利用が268 µsで、約29%短縮。8行では差の小さい条件もあった。
Editor／ClientSimでも別途9,600バッチを測り、同じ128行は539 µs対381 µsだった。環境ごとに分けて集計した。
[実クライアントの全比較と環境条件](2026-10-27-udon-string-build-t228/client-results.md)も参照。

`sb.Append(a); sb.Append(b);` と `sb.Append(a).Append(b);` の速度順位は条件により入れ替わった。
チェーンにしてもAppendの呼び出し回数は減らない。
ステータス列挙ではStringBuilderを候補にし、個別呼び出しとチェーンは読みやすさで選ぶ、という判断を勧める。
全体が定数の式はコンパイル時に畳み込まれ、連結そのものを実行しなかった。

## 問いと比較の範囲

仮説は「UdonではAppend呼び出しの費用が大きく、通常連結からStringBuilderへ変えてもあまり変わらない」である。
この仮説を、定数式、実行時の文字列、数値を含むステータス列挙に分けて調べた。
仮説は短い8断片では概ね観測と合うが、32・128行の統計表示には当てはまらなかった。

| workload | 出力 | 計測内の数値変換 |
| --- | --- | --- |
| 0: literal | `"Token0\|" + … + "Token7\|"`、56文字 | なし。通常式の定数畳み込みを観測する対照 |
| 1: runtime strings | Startで作った文字列配列8要素、同じ56文字 | なし |
| 2: preconverted stats | `Stat0: 37\n` …、8／32／128行 | なし。数値文字列を計測前に準備 |
| 3: numeric stats | workload 2と完全に同じ出力 | 通常連結は`int.ToString()`、SBは`Append(int)` |

値は`37 + 行番号 * 17`、ラベルは`Stat`と行番号である。
8／32／128行の長さはそれぞれ84／370／1621文字。
workload 2は連結方法を比べる条件であり、workload 3は利用可能な型付きAppendも含めた実装方法の比較である。
後者の差を「Append(string)の費用だけ」と解釈しない。

| case | 連結方法 | 生成／初期化 | 最終文字列 |
| --- | --- | --- | --- |
| baseline | 準備済みの期待文字列を返す | なし | 既存string |
| plus_expression | 8断片は一つの`+`式。統計は行ごとに`text += label + ": " + value + "\n"` | `text = ""` | 最終string |
| plus_steps | 断片ごとに`text += …` | `text = ""` | 最終string |
| sb_new_split / chain | 個別Append／チェーン | 毎回`new StringBuilder()` | 毎回`ToString()` |
| sb_capacity_split / chain | 同上 | 毎回`new StringBuilder(出力長)` | 毎回`ToString()` |
| sb_reuse_split / chain | 同上 | 計測外で容量確保、毎回`Clear()` | 毎回`ToString()` |
| clock | Runのディスパッチだけで戻る時計対照 | なし | なし |

SBはStringBuilderの略。
容量指定は期待出力長を事前に知る理想的な条件であり、実装中にその長さを求める処理の費用は含まない。
再利用SBは各条件の前に作り直し、ウォームアップ後の安定した容量を測った。
初回拡張、異常に長い文字列を作った後の保持容量、長期間のGCは対象外である。
CSVの`capacity`列は再利用SBの容量であり、新規生成caseそれぞれの割り当て量ではない。

## Editorの実測結果

全9,600バッチ、各条件・各caseにつき60サンプル。
5バッチのウォームアップを除き、30バッチを2巡した。
2巡目はcase順を反転し、各バッチでも開始位置を回転させた。
さらに同じ仕事を4倍の反復数で測った。
通常C#のproxyメソッドを直接呼ばず、UdonBehaviourのイベントから実行した。
VMの型は`VRC.Udon.VM.UdonVM`だった（[runtime-metadata.txt](2026-10-27-udon-string-build-t228/runtime-metadata.txt)）。

次の表は大きい反復数の**生の中央値µs／文字列1件**である。
共通のローカルメソッド呼び出し・出力保持・Length加算を含む。
baseline差し引き後の値、p95、最小・最大、MAD、各巡の中央値は[summary.csv](2026-10-27-udon-string-build-t228/summary.csv)に全件を残した。

| 方法 | 定数8断片 N=256 | 実行時文字列8断片 N=256 |
| --- | ---: | ---: |
| 通常の式 | 1.956 | 8.630 |
| 個別`+=` | 6.468 | 9.277 |
| SB新規・個別 | 6.826 | 9.457 |
| SB新規・チェーン | 6.839 | 9.583 |
| SB容量指定・個別 | 6.018 | 8.648 |
| SB容量指定・チェーン | 5.904 | 8.750 |
| SB再利用・個別 | 6.109 | 8.843 |
| SB再利用・チェーン | 6.151 | 8.745 |

全定数の通常式は、生成Assemblyで結合済み文字列をCOPYして返した。
実行時文字列8断片では、SB再利用／通常式の中央値比は1.025、小さいN=64では1.029だった。
この短い出力ではSBによる速度改善は確認できない。
数個の文字列を組み立てるためだけにSBへ置き換える必要はない。

| 入力 | 行数 | N | 通常の式 | SB新規・個別 | SB容量指定・個別 | SB再利用・個別 | 再利用／通常式 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 変換済み文字列 | 8 | 64 | 31.591 | 26.619 | 25.466 | 25.542 | 0.809 |
| 変換済み文字列 | 32 | 16 | 111.412 | 92.513 | 87.078 | 87.178 | 0.782 |
| 変換済み文字列 | 128 | 8 | 453.894 | 339.069 | 326.469 | 337.137 | 0.743 |
| intを都度変換 | 8 | 64 | 33.442 | 28.866 | 27.241 | 27.588 | 0.825 |
| intを都度変換 | 32 | 16 | 130.203 | 102.656 | 99.747 | 100.291 | 0.770 |
| intを都度変換 | 128 | 8 | 538.631 | 397.400 | 388.919 | 381.450 | 0.708 |

小さいNでもSB再利用／通常式は0.705〜0.822で、複数行では両巡ともSB再利用が速かった。
変換済み文字列でも差が残るので、数値変換だけでは結果を説明できない。
一方、容量指定や再利用の差は通常連結との差ほど大きくなく、caseや巡によってばらついた。
今回の測定だけで「常に再利用が最速」とはしない。

### チェーン化による一貫した短縮は見られなかった

全条件・容量条件でのチェーン／個別の中央値比は0.964〜1.072だった。
caseによっては小さい差が現れたが、実行巡やNを通じた一定の優位性はない。
[comparisons.csv](2026-10-27-udon-string-build-t228/comparisons.csv)に同一config・run・batchの差、各巡の比、bootstrap区間を示した。
区間は同一セッション内の参考値で、独立したPC間の信頼区間でも、多重比較を調整した検定でもない。

Assemblyでは、個別／チェーンとも8断片でAppendを8回、統計の1行で4回呼ぶ。
チェーンは直前のAppendの戻り値を次のreceiverに使い、個別呼び出しは元の変数をreceiverに使った。
新規・容量指定の対応メソッドは全分岐を含めた命令数もそれぞれ同じだった。
これは構造の説明であり、命令数を実測時間の代わりには使っていない。
静的集計は[assembly-summary.csv](2026-10-27-udon-string-build-t228/assembly-summary.csv)を参照。

### ばらつきと長いバッチも保存した

時計対照の中央値は概ね約5 µs／バッチで、ゼロtickはなかった。
計時には`Stopwatch.GetTimestamp()`と`Frequency=10,000,000`を使い、`IsHighResolution=True`を確認した。
100 nsはtickの単位であり、この実験全体の精度を100 nsと主張するものではない。

最大バッチは変換済み128行・個別`+=`・N=8の169.084 msだった。
数十〜百msの外れ値も除外していない。
GC、OSの割り込み、他アプリの負荷のどれが原因かは記録だけでは特定できない。
割り当て量・GC回数を測っていないため、「GCが何%減った」とは結論しない。
計測外のCSV蓄積で生じる割り当ても、後続バッチのGCへ影響しうる。
通常の中央値が短くても、文字列を一斉更新する場面では最悪時間も別に確認する必要がある。

数値128行・N=8の代表値を示す。各60バッチで、単位は表の列ごとに異なる。

| 方法 | 中央値 ms/batch | p95 ms/batch | 最小／最大 ms/batch | baseline差分 µs/文字列 |
| --- | ---: | ---: | ---: | ---: |
| 通常の式 | 4.309 | 5.263 | 3.714／6.900 | 535.781 |
| SB新規・個別 | 3.179 | 6.760 | 2.778／14.413 | 394.563 |
| SB容量指定・個別 | 3.111 | 5.387 | 2.681／11.794 | 385.950 |
| SB再利用・個別 | 3.052 | 5.485 | 2.728／30.424 | 378.606 |

## 他ワールドでの実装判断

数個の断片や短いメッセージには`+`を使ってよい。
全定数なら一つのリテラルまたは定数式にする。
実クライアントの32・128行のステータス／統計では、StringBuilderを使う理由があった。8行は差の小さい条件があり、置き換えの効果を別途確かめる。
既知のラベルと整数値を順にAppendし、最後に一度だけToStringしてUIへ渡す構成が候補になる。

```csharp
// UdonSharpBehaviour内。使用頻度・同時利用の有無に応じてフィールドで保持する。
private StringBuilder _statusBuilder;

private void Start()
{
    _statusBuilder = new StringBuilder(2048); // 予想出力長。実際の上限に合わせる。
}

private string BuildStatus(string[] labels, int[] values)
{
    _statusBuilder.Clear();
    for (int i = 0; i < labels.Length; i++)
        _statusBuilder.Append(labels[i]).Append(": ").Append(values[i]).Append("\n");
    return _statusBuilder.ToString();
}
```

この例は同じ長さの配列を渡す前提で、測定済みAPIを使った適用例である。
他ワールドへの導入・動作検証は行っていない。
フォーマット指定、小数、ローカライズ、装飾タグ、他Behaviourからの値取得、TMPのレイアウト・描画は測定外であり、実際のUI更新時間を別に測る必要がある。
再利用する場合は、処理の途中で同じbuilderを別用途から使わないようにする。
更新頻度や表示行数を変える設計上の判断は、今回の連結方法の比較とは別である。

## 再現条件と証拠

| 項目 | 条件 |
| --- | --- |
| Git | main、計測時の基準commit `626f732055ad53ae8eb2dbf33cbf61af46030d95`、計測時の実験コードは未コミット |
| Unity | 2022.3.22f1 (887be4894c44) |
| VRChat Worlds / Base | 3.10.5、UdonSharpはSDK同梱（assembly version 0.0.0.0） |
| OS / CPU / GPU | Windows 11 10.0.26200 x64 / Core i7-9700K / RTX 3070 Ti |
| 実行 | Editor、ClientSim、ローカル1人、同期データ・ネットワークイベントなし、sync None |
| 描画設定 | Game View 1220×687、targetFrameRate 72、VSync 0、高パフォーマンス電源設定 |
| 観測設定 | Profiler無効、Deep Profileを用いた収集なし、UdonDebugLogging=True、計測内ログなし |
| 同時負荷 | ほかのUnity Editorを2つ開いたまま。OS・他アプリの負荷を隔離していない |
| 反復数 | 8断片64／256、統計8行16／64、32行4／16、128行2／8。1バッチ／フレーム |
| 出力検証 | 別途準備した期待値と最後の文字列の完全一致、全反復のLength和。事前検証では整数値を変えた2入力で288件一致 |

実測は2026-10-27 13:17〜13:20 JST。
計測区間には生成・Clear・Append・ToStringを条件どおり含め、入力生成、期待値作成、ログ、CSV文字列化、ファイル出力を除いた。
各反復のcase選択は行わず、バッチ外側のswitchで選ぶ。
すべてのcaseで同じ形のローカルメソッド呼び出し、結果保持、Length加算を行う。
baseline差分は同じconfig・run・batchで引き、負値をゼロに丸めない。

| 証拠層 | 到達点 |
| --- | --- |
| C# compile | 通過 |
| UdonSharp Editor compile | 通過。ctor()／ctor(int)／Append(string)／Append(int)／Clear／ToStringを生成Assemblyで確認 |
| Editor／ClientSim Udon VM | 9,600バッチ、288事前チェック、Failures=0 |
| VRChat Build & Test / PC Desktop | 完走9,600バッチ、Failures=0。全行を回収し独立検証。Editor結果と合算しない |
| PC VR／HMD／Quest／複数人／長時間／GC量 | 未測定 |

生データの完全性は[validation.json](2026-10-27-udon-string-build-t228/validation.json)を参照。
9,600行の全組み合わせ、重複、順序、N、出力長の期待値をPythonで独立に再確認した。
初期状態の既存生成アセット13件は最終確認でも全件SHA-256が一致した。SDKコンパイルだけの変更を整理し、既存の差分・stagingを保全した（[保全記録](2026-10-27-udon-string-build-t228/preservation-evidence.json)）。
push・公開は行っていない。

### 再実行手順

1. 作業ツリーと未保存シーンを確認し、対象のUnityでC#コンパイルを待つ。既存シーンに未保存変更がある場合はツールが停止する。
2. 初回だけ`Tools > String Build Experiment > Create Scene`。同名シーンがある場合は既存の`Assets/App/World/Scenes/StringBuildExperiment.unity`を開く。
3. Editor計測はPlay。120フレーム待機・出力事前検証の後に自動開始する。`Live Status`で進捗を確認し、`Complete=True`・`Rows=9600`・`Failures=0`を待つ。
4. Play中に`Export Live Results`を実行し、その後Playを終了する。既存CSVを残したい場合は実行前に別フォルダへ保管する。
5. `python Documents/Reports/2026-10-27-udon-string-build-t228/analyze.py`で全件検証と集計を再生成する。静的集計は同フォルダの`analyze_assembly.py`を使う。
6. 実クライアントはSDKパネルを開き、この専用シーンで`Build and Test`。測定完了後にCSVを0.3秒間隔・1800文字ずつログへ出す。`StringBuildCSV-END`を待ち、`python …/extract_client.py <今回のoutput_log.txt>`、続いて`python …/analyze.py raw-client.csv`を実行する。

コードは[StringBuildBenchmark.cs](../../Assets/App/World/UdonSharp/StringBuildBenchmark.cs)、Editor補助は[StringBuildExperiment.cs](../../Assets/App/Editor/StringBuildExperiment.cs)。
生成ProgramAsset、SerializedUdonProgramsとそれぞれのmeta、専用sceneも成果物に含む。
実験補助はシーン作成・計測・出力用として残し、一時的な固定値変更ツールは追加していない。
クライアントCSV出力の追加はEditor計測後で、計測ループ・連結メソッドは変更していない。
Editor実測時の正確なAssemblyは[StringBuildBenchmark-editor-measured.uasm](2026-10-27-udon-string-build-t228/StringBuildBenchmark-editor-measured.uasm)に保存した。

## 一次資料と解釈の限界

- [VRChat SDK 3.7.1リリースノート](https://creators.vrchat.com/releases/release-3-7-1/)はStringBuilderの公開を記している。今回利用した個々のメンバーは、導入済み3.10.5でのコンパイルと実行を根拠とした。
- [VRChat UdonSharp Performance Tips](https://creators.vrchat.com/worlds/udon/udonsharp/performance-tips/)はUdonと通常C#の性能差や呼び出し境界の注意を記している。そこで挙げられた一般的な倍率を、この連結比較の倍率には流用していない。
- [Microsoft StringBuilderのAPI](https://learn.microsoft.com/en-us/dotnet/api/system.text.stringbuilder)は可変文字列、容量、Clear、Append、ToStringの仕様を確認する資料である。一般の.NETでの性能説明をUdonの実測結果として扱わない。
- [VRChat Class Exposure Tree](https://creators.vrchat.com/worlds/udon/udonsharp/class-exposure-tree/)は公開API確認の入口。最終的な利用可否は対象SDKの生成プログラム・実行結果で判断する。
- 導入SDKの`Packages/com.vrchat.worlds/Integrations/UdonSharp/Editor/Compiler/`と実測Assemblyを併読した。定数畳み込みや複数の文字列加算externを確認したが、静的命令数から実行時間を推定していない。

単一PC・同一セッション内の比較である。
8断片より短い／長い固定式の網羅、数千行や巨大文字列での損益分岐点、C#版との速度比、純粋なextern呼び出しだけの費用は測っていない。
文字数だけから別のレイアウトや機器の実行時間を予測しない。
