# Tama

弾幕のためだけの小さな言語。記事では設計と状態機械を中心に説明しているので、ここではコンパイラの中身を順に説明する。読む順番は次のとおり。

| 段階 | ファイル | 内容 |
|---|---|---|
| 字句解析 | [compiler/lexer.mll](compiler/lexer.mll) | ソースをトークンに区切る。単位付きの数もここで処理する |
| 構文解析 | [compiler/parser.mly](compiler/parser.mly) | Menhir の文法。構文木（[compiler/ast.ml](compiler/ast.ml)）を作る |
| 型検査と脱糖 | [compiler/typecheck.ml](compiler/typecheck.ml) | 単位を検査し、変数をスロットに割り当て、構文を単純な形に直す |
| コード生成 | [compiler/codegen.ml](compiler/codegen.ml) | 状態機械の LLVM IR をテキストで書き出す |
| ランタイム | [runtime/tama_rt.c](runtime/tama_rt.c) | 弾を出す命令と色の計算。UniBridge を通して C# を呼ぶ |

例は [examples/ore.tama](examples/ore.tama)、エラーの例は [tests/](tests) にある。

## 動かす

```sh
scripts/build_tama.sh                          # リポジトリのルートで実行する
cd langs/tama
dune exec compiler/tamac.exe -- examples/ore.tama -o build/ore.ll   # LLVM IR を見る
dune exec compiler/tamac.exe -- tests/angle_plus_seconds.tama       # エラーを見る
clang -O2 -Wno-override-module build/ore.ll runtime/tama_rt.c tests/harness.c -o build/harness
./build/harness                                # fork と巻き戻しのテスト
```

macOS で SDK のエラーが出たら、先に `source scripts/env.sh` を実行する。

## 字句解析: 単位付きの数

`2m/s`、`0.2s`、`500ms`、`2frames`、`30deg`、`50%` は、字句解析で 1 つのトークンになる。値は基本単位（m、秒、ラジアン）に直し、次元を付ける。60 フレームを 1 秒とする。`#ff3366` は色のトークンになる。

## 構文解析

文法は約 80 行。文の区切りは不要で、弾を出す命令の引数はカンマでつなぐ。`(a → b) at t` の `at` は優先順位をいちばん低くしているので、`at t + 1` は `at (t + 1)` と読む。

## 型検査: 単位の次元解析

次元は長さ（m）、時間（s）、角度（deg）の指数の組で、`m/s` は `{ m = 1; s = -1; deg = 0 }` になる。

| 演算 | 規則 |
|---|---|
| `+` `-` | 同じ次元どうしだけ |
| `*` | 指数を足す |
| `/` | 指数を引く。結果は常に浮動小数点数 |
| 色 `+` 角度 | 色相を回して、色になる |

実際のエラーは次のとおり。

```text
turn: 7deg + 0.2s    error: 単位が合いません: deg + s
speed: 2.0           error: ring の speed は m/s です（単位なし が渡されました）
wait 10              error: wait には時間の単位を付けてください（例: 0.2s、10frames）
repeat forever { aim speed: 3m/s }
                     error: repeat forever に every も wait もないと、1 フレームの中で永久に回ってしまいます
```

最後のものは型ではなく、1 フレームごとに止まるという言語の性質から来る検査である。

## 脱糖

後の段が知っている制御構文は `while`、`repeat`、`wait`、`fork` だけになる。

| 書いたもの | 直したもの |
|---|---|
| `every I` | ループの最後に `wait I` を足す |
| `until 条件 { }` | 条件を反転した `while` |
| `during D every I as t { }` | `start = frame; while frame - start < D { t = (frame - start) / D; ...; wait I }` |
| `色 + 角度` | `tama_rt_hue` の呼び出し |
| `(a → b) at t` | `tama_rt_mix` の呼び出し |

## 状態機械と fork

すべての変数をフレーム構造体のスロットに置く。スロット 0〜2 は予約（再開位置の番号、残り待ちフレーム、生きているか）である。状態全体は「共通のフレームカウンタ 8 バイト＋フレーム × 分身の数」で、`tama_state_size()` はこの大きさ（1 スロット 8 バイトとした見積もり）を返す。

`wait` は残りフレームと再開位置の番号を書いて `return` し、次に呼ばれたら入口の `switch` で戻ってくる。`fork` は自分の再開位置を `fork` の直後にして、`llvm.memcpy` でフレームを写す。`fork` の中の `fork` は禁止しているので、分身の数の上限はコンパイル時に決まる。

記録した状態は、同じコンパイル結果でしか使えない。コードを変えるとスロットの並びが変わる。
