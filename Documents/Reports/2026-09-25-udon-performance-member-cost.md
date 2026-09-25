---
created_at: "2026-09-25T17:12:53+09:00"
updated_at: "2026-09-25T17:12:53+09:00"
category: report
tags: [udon, performance, benchmark, vrchat]
status: completed
experiment_id: UDON-PERF-20260925
measured_at: "2026-09-25T17:05:32+09:00"
environment: "Unity 2022.3.22f1 / VRChat Worlds SDK 3.10.5 / WindowsEditor"
---

# Udon のメンバー公開範囲と呼び出し境界の性能測定

## 結論

同じ UdonSharpBehaviour 内で直接呼び出す範囲では、public と private のメソッド、フィールド、property の中央値に、この測定で再現できる大きな差は出なかった。

別の UdonBehaviour から public メソッドを直接呼び出すと、同じ処理を同一 Behaviour 内で呼ぶ場合より、1,000 回あたり約 2.3 倍から 5.5 倍の時間を要した。

外部 Behaviour の public field は直接アクセスより約 1.6 倍から 2.1 倍、public property は約 3.9 倍から 4.0 倍だった。

`ref` は private と public の同一 Behaviour 内で差がなく、外部呼び出しでも `out` より常に安いとはいえない。

static method は同じ形の instance method より一貫して安いとはならず、static method 内のローカル宣言数を増やした場合も、通常の method と同じく時間が増えた。

bool の読み取りが頻繁な GC を発生させる証拠は得られなかった。
この実験は GC bytes や GC collection count を測っていないため、「bool は GC を起こさない」と断定するものでもない。

## 測定対象

`UdonPerformanceBenchmark` に、次の 52 ケースを実装した。

- private と public の method 呼び出し
- 引数なしと引数あり
- 戻り値ありと `void`
- `out` と `ref`
- private と public の static method
- instance method と static method の内部ローカル宣言数 0、1、4、8
- private と public の field 読み取りと書き込み
- private と public の property 読み取りと書き込み
- private と public の bool 読み取り
- bool と同じ分岐を `int` フラグで表した場合
- 別の UdonBehaviour にある public method の直接呼び出し
- 別の UdonBehaviour にある public field と public property の直接アクセス
- 別の UdonBehaviour に対する `SendCustomEvent`

外部呼び出しでは、ベンチマーク開始時に対象 Behaviour の参照を取得し、その参照を各反復で再利用した。
したがって、対象検索や `GetComponent` のコストは測定区間に含めていない。

## 測定方法

Unity Editor 上の Udon VM で測定した。

`Stopwatch.GetTimestamp` で各ケースの反復部分だけを囲み、1 バッチを 1,000 回として、各ケースを 2 run で 30 バッチずつ測定した。

各 run の先頭 5 バッチはウォームアップとして捨て、合計 3,120 行の生データを保存した。

run 1 はケース番号の昇順、run 2 は降順にして、ケース順による偏りを抑えた。

タイマー区間には、結果文字列の生成、CSV への書き込み、ログ出力、UI 更新を含めていない。

すべての行で、計算結果と期待値が一致した。

生データは [raw-editor.csv](2026-09-25-udon-performance-member-cost/raw-editor.csv) に保存している。

以下の時間は、特記しない限り「1,000 回あたりの 1 バッチの中央値、単位は ms」である。

## 同一 Behaviour 内の public と private

| ケース | private | public | public / private |
| --- | ---: | ---: | ---: |
| 引数なし、戻り値あり | 1.50 | 1.49 | 0.99 |
| 引数あり、戻り値あり | 1.50 | 1.50 | 1.00 |
| 引数なし、`void` | 1.41 | 1.39 | 0.99 |
| 引数あり、`void` | 1.37 | 1.38 | 1.01 |
| `out` | 1.60 | 1.59 | 0.99 |
| `ref` | 1.58 | 1.58 | 1.00 |
| static、引数あり、戻り値あり | 1.49 | 1.49 | 1.00 |
| static、`out` | 1.57 | 1.58 | 1.01 |
| static、`ref` | 1.58 | 1.58 | 1.00 |
| field 読み取り | 1.49 | 1.50 | 1.01 |
| field 書き込み | 1.19 | 1.20 | 1.01 |
| property 読み取り | 1.52 | 1.50 | 0.99 |
| property 書き込み | 1.50 | 1.50 | 1.00 |
| bool 読み取り | 1.60 | 1.61 | 1.01 |
| `int` フラグ読み取り | 1.86 | 1.87 | 1.01 |

public field や public property が、同一 Behaviour 内の直接アクセスだけで private より明確に高くなる結果は得られなかった。

この結果は「public の API 表面を増やしても常に無料」という意味ではない。
VRChat 公式の性能ガイドは、別スクリプトから呼ばれない method を private にし、public method を減らすと Custom Event の探索対象を減らせると説明している。
したがって、外部から呼ぶ必要のないものは private にし、外部境界だけを public にする設計を維持する。

## 別 Behaviour からの public 呼び出し

同じ処理の同一 Behaviour 内 public 呼び出しと、別 Behaviour への public 呼び出しを比較した。

| ケース | 同一 Behaviour の public | 外部 Behaviour の public | 比率 |
| --- | ---: | ---: | ---: |
| 引数なし、戻り値あり | 1.49 | 6.21 | 4.17 |
| 引数なし、`void` | 1.39 | 3.71 | 2.67 |
| 引数あり、戻り値あり | 1.50 | 7.10 | 4.73 |
| 引数あり、`void` | 1.38 | 5.03 | 3.64 |
| `out` | 1.59 | 8.65 | 5.44 |
| `ref` | 1.58 | 7.24 | 4.58 |
| public field 読み取り | 1.50 | 2.48 | 1.65 |
| public field 書き込み | 1.20 | 2.55 | 2.13 |
| public property 読み取り | 1.50 | 6.00 | 4.00 |
| public property 書き込み | 1.50 | 5.78 | 3.85 |

外部 method のコストは、引数、戻り値、`out`、`ref` の組み合わせで変わった。
ただし、この結果から特定の引数形式がすべての処理で最速になるとはいえない。

外部 field は property より明確に短かった。
property の外部アクセスは、この環境と UdonSharp コンパイル結果では method 呼び出しに近い時間になった。

`SendCustomEvent` は 1,000 回あたり 3.26 ms だった。
同じ対象 method を直接 public 呼び出ししたケースとは呼び出し形式と method 本体が異なるため、単純な優劣ではなく、Custom Event も外部境界のコストを持つ例として扱う。

## ローカル宣言数と static

通常の private method では、内部ローカル宣言数 0 の中央値が 1.51 ms、8 の中央値が 2.19 ms だった。
p95 は 1.82 ms から 2.67 ms に増えた。

static method では、内部ローカル宣言数 0 の中央値が 1.49 ms、8 の中央値が 2.19 ms だった。
p95 は 2.00 ms から 3.02 ms に増えた。

ここでいう `private_static_local_N` は、C# の static local variable ではなく、static user method 内に置いたローカル宣言数を表す。

現在の UdonSharp コンパイラでは user-defined type の static field が未対応だったため、static field を使ったケースは追加していない。

static method の引数なし戻り値ありケースは、static field の代わりに定数 `1` を返す実装になっている。
このケースは static field の性能比較ではなく、定数を返す static method の比較として扱う。

VM の説明では、Udon は通常の C# のようなローカル変数ではなく、オブジェクト上の変数を中心に実行すると説明されている。
そのため、ローカル宣言数の結果を C# のスタック変数の一般則として扱わず、現在の UdonSharp 生成コードに対する測定結果として利用する。

## bool と GC

bool 読み取りの中央値は private が 1.60 ms、public が 1.61 ms だった。
比較用の `int` フラグ読み取りは private が 1.86 ms、public が 1.87 ms だった。

この測定では bool のほうが速く、bool が頻繁な GC を発生させる傾向は見えなかった。

ただし、測定したのは経過時間だけで、割り当てバイト数、GC collection count、boxing の有無は記録していない。
「bool だから GC が起きる」という主張を支持する測定結果はなく、plain な bool 読み取りと、object 化、文字列化、配列生成、外部 API が値を変換する処理を同一視しない。

## 主張ごとの判定

| 主張 | 判定 | 根拠 |
| --- | --- | --- |
| `ref` はコストを下げる | 支持しない | 同一 Behaviour 内の private と public はともに 1.58 ms。外部では `ref` 7.24 ms、`out` 8.65 msだが、形式と生成コードが異なるため一般則にできない。 |
| static のローカル宣言は安い | 支持しない | static method でも宣言数 0 から 8 で中央値 1.49 ms から 2.19 msに増えた。 |
| 同じ class の public field は private field より高い | この測定では支持しない | 読み取り 1.49 ms 対 1.50 ms、書き込み 1.19 ms 対 1.20 ms。 |
| property は field と同じコスト | 外部境界では支持しない | 外部 field 読み取り 2.48 msに対して property 読み取り 6.00 ms。 |
| bool は頻繁な GC を起こす | 未確認 | 時間差と GC 指標を分けて測る必要がある。今回の GC 指標は未測定。 |
| public は常に private より遅い | 支持しない | 同一 Behaviour 内の全ペアで比率は 0.99 から 1.01。public API の広さや外部境界のコストとは別の問題である。 |

## 他のワールドに適用する指針

- 外部から呼ぶ必要がない method、field、property は private にする。
- 毎フレームや大量ループから別 Behaviour の public member を個別に呼ばず、対象参照をキャッシュし、呼び出し回数を減らす。
- 外部境界を越える必要がある場合は、細かい field、property、method を大量に往復させず、必要なデータをまとめて扱う API を候補にする。
- `ref` や `static` を、性能だけを理由に選ばない。
- ローカル宣言そのものを機械的に削るより、ループ回数、毎フレームの呼び出し回数、同じ値の再計算を先に確認する。
- bool は plain な分岐値として使用し、GC を避けたい箇所では boxing、文字列連結、配列生成、不要な変換を別途確認する。
- ネットワークイベントや外部公開 API に必要な public 宣言は、アクセス修飾子の局所的な差ではなく、公開境界として設計する。

## 検証段階

| 段階 | 結果 | 内容 |
| --- | --- | --- |
| C# コンパイル | 実施済み | 2 スクリプトの compile feedback にエラーなし。 |
| UdonSharp コンパイル | 実施済み | ProgramAsset と Serialized Udon Program を生成し、Unity Console のエラー 0。 |
| Unity Editor Play | 実施済み | 2 run、3,120 行、checksum 不一致 0。 |
| ClientSim | 未実施 | `Packages/manifest.json` に `com.vrchat.clientsim` がなく、個別テストをしていない。 |
| VRChat Build & Test | 未実施 | Build Settings の対象シーンを追加していないため、実行していない。 |
| VRChat 実機、クライアント | 未確認 | Editor Udon VM の結果であり、VRChat クライアントの実行結果ではない。 |

Editor Play 中に既存シーン由来の Event System 重複警告が出たが、ベンチマークの Udon エラーではない。

## 参照した公式文書

- [UdonSharp Performance Tips](https://creators.vrchat.com/worlds/udon/udonsharp/performance-tips/)
- [UdonSharp Class Exposure Tree](https://creators.vrchat.com/worlds/udon/udonsharp/class-exposure-tree/)
- [Udon VM and Assembly](https://creators.vrchat.com/worlds/udon/vm-and-assembly/)
- [UdonSharp](https://creators.vrchat.com/worlds/udon/udonsharp/)
- [Udon Networking Events](https://creators.vrchat.com/worlds/udon/networking/events/)

`Udon VM and Assembly` は Udon の動作を説明する公式サイト上のコミュニティ作成ページであり、VM 実装の割り当て動作を保証する資料としては扱っていない。

## 制限事項

測定対象は 1 台の Windows Editor 環境、Unity 2022.3.22f1、VRChat Worlds SDK 3.10.5 に限られる。

Editor Udon VM と VRChat クライアントの実行時間は同一とは限らない。

この実験は単一プロセス内の UdonBehaviour 間呼び出しであり、ネットワーク同期、ownership transfer、network event の送受信時間は測っていない。

GC の頻度を結論にするには、VRChat クライアント上で Unity Profiler または同等の実行時計測を追加し、割り当てバイト数と collection count を別実験として記録する必要がある。
