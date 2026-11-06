# Polyglot Boss Rush

Unity Sunday Book Vol. 2（技術書典21）「Unity なら C# を使わないといけない理由ってある？ ないよなぁ!!!!」のサンプルプロジェクトです。

8 体のボスを、それぞれ別の言語で書いた弾幕シューティングです。ボスを倒すたびに言語が変わり、Unity とのつなぎ方も変わります。

| # | 言語 | ボス | つなぎ方 | 「待つ」の書き方 | 巻き戻し | ソース |
|---|---|---|---|---|---|---|
| 1 | C# | Spiral | 本家 | `yield return` | ○（反則技） | [CSharpBoss.cs](Assets/PolyglotBossRush/Scripts/Bosses/CSharpBoss.cs) |
| 2 | F# | Pattern Algebra | IL（DLL） | `seq { yield ... }` | × | [langs/fsharp](langs/fsharp/BossFSharp/Library.fs) |
| 3 | Rust | Gravity Wells | C ABI | （弾幕エンジンも担当） | ○ | [langs/rust](langs/rust/src) |
| 4 | TypeScript | CSS Fireworks | QuickJS-ng を Rust に組み込み | `async` / `await` | × | [langs/typescript](langs/typescript/src) |
| 5 | Python | Bullet Banner | CPython を埋め込み | generator と `yield from` | × | [langs/python](langs/python) |
| 6 | OCaml | Fibonacci Sunflower | Wasm（wasm_of_ocaml → wasmtime） | effect handler | × | [langs/ocaml](langs/ocaml/boss.ml) |
| 7 | Go | Goroutine Swarm | Wasm（GOOS=wasip1 → wasmtime） | 1,000 個の goroutine とチャネル | × | [langs/go](langs/go/boss.go) |
| 8 | Tama（オレオレ言語） | The Last Boss | LLVM でネイティブコンパイル | `every 0.2s` と `during 2s`、中断状態を写す `fork` | ○ | [langs/tama](langs/tama) |

## 時間の巻き戻し

REWIND を押しているあいだ、直近 5 秒の状態をさかのぼります。弾や自機は C# 側で保存できますが、ボスの「コルーチンの途中の状態」を保存して戻せるかどうかは言語しだいです。

| 言語 | 巻き戻し | 理由 |
|---|---|---|
| Tama | ○ | すべての変数と「どこで止まっているか」を、大きさの決まったフレーム構造体に置くように言語を設計した（ラスボスは `fork` の分身 3 体分を含めて 272 バイト）。`memcpy` するだけで保存も復元もできる |
| Rust | ○ | ボスの状態は普通の struct（フレーム数）なので、そのまま保存できる |
| C# | ○（反則技） | `yield` の状態はコンパイラが作ったクラスのフィールドにある。リフレクションで `MemberwiseClone` を呼んで丸ごと複製する |
| F# | × | `seq { }` は `yield!` のたびに入れ子の列挙子を作って共有するので、浅いコピーでは戻せない |
| TypeScript | × | `await` の中断状態は QuickJS のヒープ上のクロージャとジョブキューにあり、取り出す手段がない |
| Python | × | generator はコピーも pickle もできない |
| OCaml | × | OCaml 5 の継続は一度しか再開できない（one-shot）。実体も Wasm の GC ヒープにある |
| Go | × | 1,000 個の goroutine のスタックは、Wasm の線形メモリの中で Go のランタイムが管理している。ボスの分だけを取り出す手段はない |

### 処理系の目で見た 8 つの中断状態

| 言語 | 中断状態の実体 | 写せない／写せる理由 |
|---|---|---|
| C# | コンパイラが作るイテレータのクラス。`yield` をまたぐローカル変数はフィールドに持ち上がる | フィールドが値だけなら、浅いコピーで済む |
| F# | `seq` の列挙子の連鎖。`yield!` のたびに、内側の列挙子を外側が持つ | 浅いコピーでは内側が共有される |
| TypeScript（QuickJS） | `await` の中断状態は Promise に登録されたクロージャで、ジョブキューから呼ばれる | QuickJS のヒープの中にあり、取り出す API がない |
| Python | generator が持つフレームオブジェクト（ほかの Python オブジェクトへの参照を含む） | コピーも pickle もできない |
| OCaml 5 | effect で捕まえた継続。wasm_of_ocaml では CPS 変換されたクロージャとして Wasm の GC ヒープにある | 一度しか再開できない（one-shot）。時間を巻き戻すのは、同じ継続を 2 回再開することにあたる |
| Go | goroutine ごとのスタック。Go の Wasm 版では線形メモリにあり、切り替えは Wasm のスタックをほどいて行う | 1 体分だけを取り出す API はない。線形メモリを丸ごと写す力ずくの方法なら可能性がある（未検証） |
| Rust | 中断の構文を使わず、`frame: u32` の構造体 | ただの値 |
| Tama | 大きさが決まった、ポインタを含まないフレーム | `memcpy` で何度でも写せる（multi-shot）。言語自身もこれを使って `fork` を実装している |

### ランタイムの大きさと 1 フレームの時間

Galaxy S24 での値。1 フレームの時間は 5 秒目の平均で、弾幕の中身が違うので、言語の速さの比較にはならない。

| ボス | ランタイム | 追加するファイル（圧縮前） | 1 フレームの時間 |
|---|---|---|---|
| TypeScript | QuickJS | `libunihub.so` 4.1MB（wasmtime と共有）、スクリプト 2.7KB | 約 44µs |
| Python | CPython | `libpython` 5.9MB、標準ライブラリ 5.0MB、スクリプト 1.5KB | 約 2µs |
| OCaml | wasmtime | `libunihub.so` を共有、`.cwasm` 1.2MB | 約 25µs |
| Go | wasmtime | `libunihub.so` を共有、`.cwasm` 3.9MB | 約 170µs |

実装は [BossRushGame.cs](Assets/PolyglotBossRush/Scripts/Core/BossRushGame.cs) の「時間の巻き戻し」と、各ボスの `SaveState` / `LoadState` を見てください。

## しくみ

```
Unity（C#、IL2CPP）
 ├─ BossRushGame ─ 自機、ボスの順番、HUD
 ├─ BulletWorld ──── P/Invoke ──→ Rust の弾幕エンジン（rayon で並列に 10 万発を更新）
 ├─ CircleRenderer ─ StructuredBuffer を読むシェーダーで、弾を 1 回の描画命令で描く
 └─ ボス
     ├─ C#、F#                 BossApi を直接呼ぶ
     └─ Rust、TS、Python、OCaml、Go、Tama
                               UniBridge（langs/common/unibridge.h）だけで Unity とやり取りする
```

ネイティブのボスが見る Unity の窓口は、C の関数ポインタ表 1 つだけです。

```c
typedef struct UniBridge {
    void (*spawn)(float x, float y, float angle, float speed, float accel, uint32_t rgba, void *user);
    float (*hp_ratio)(void *user);
    float (*aim_angle)(void *user);
    void *user;
} UniBridge;
```

## 動作環境

- Unity 6000.6.7f1（Android Build Support）
- 対象は **macOS（Apple Silicon）と Android（arm64-v8a）**。開発とビルドは macOS で行う
- Windows でも同じ構成で動くはずですが、確認していません（各言語のライブラリを Windows 向けにビルドするスクリプトもありません）
- macOS 版の Python のボスは Homebrew の `python@3.14` を参照します。Python が入っていない Mac では、そのボスだけ飛ばして次に進みます

ビルドに必要なツール:

| 言語 | ツール |
|---|---|
| Rust | rustup（stable）、`rustup target add aarch64-linux-android`、`cargo install cargo-ndk` |
| F# | .NET SDK 10 |
| TypeScript | Node.js（TypeScript 7 と esbuild は npm で入る） |
| Python | Homebrew の `python@3.14`（macOS 用）。Android 用は python.org の公式版をスクリプトがダウンロードする |
| OCaml | opam で OCaml 5.4、`wasm_of_ocaml-compiler`、`menhir`、`dune`。Homebrew の `binaryen` |
| Go | Go 1.25 以降 |
| Tama | OCaml と同じ。clang（Xcode Command Line Tools） |

Android の NDK は Unity に同梱のものを使います。

## ビルド

各言語のボスをビルドして `Assets/PolyglotBossRush/Generated/` に配置します（生成物は git に入れていません）。

```sh
scripts/build_all.sh
```

言語ごとに `scripts/build_rust.sh` や `scripts/build_tama.sh` を個別に実行することもできます。OCaml と Go の事前コンパイルは Rust のツールを使うので、`build_rust.sh` を先に実行してください。

そのあと Unity で `Assets/PolyglotBossRush/Scenes/BossRush.unity` を開いて再生するか、メニューの **Polyglot Boss Rush → Build macOS / Build Android** でビルドします。バッチモードでも同じことができます。

```sh
Unity -batchmode -quit -projectPath . -buildTarget Android \
  -executeMethod PolyglotBossRush.Editor.SampleBuild.BuildAndroid
```

## 操作

| 操作 | 画面のボタン | キーボード | ゲームパッド |
|---|---|---|---|
| 移動 | 左下の十字キー | 矢印キー / WASD | 左スティック |
| 時間を巻き戻す（押しているあいだ） | REWIND | Z | 左トリガー |
| 最初のボスからやり直す | RESET | R | Start |

弾は自動で撃ちます。被弾すると効果音が鳴り、火花と閃光が出て、1 秒間は無敵になります。

コマンドライン引数（Android では `adb shell am start ... -e unity "'-autoplay'"` で渡せます）:

| 引数 | 内容 |
|---|---|
| `-autoplay` | 自機を自動で動かす |
| `-boss <n>` | n 番目（0 始まり）のボスから始める |
| `-capture <dir>` | 各ボスの 5 秒目（`-capture-at` で変えられる）と、最初に被弾した直後の画面を保存し、全員分を撮ったら終了する（Android では `persistentDataPath` からの相対パス。`scripts/capture_android.sh` が撮って取ってくる） |
| `-rewind-at <秒>` | `-autoplay` のとき、各ボスのこの時刻に 1 回だけ自動で巻き戻す（`-capture` と一緒なら巻き戻し中の画面も撮る） |
| `-boss-hp <割合>` | 最初のボスの HP をこの割合（0〜1）から始める。後半のフェーズ（Tama の `fork` など）を撮るため |
| `-capture-at <秒>` | `-capture` で撮る時刻（既定は 5 秒目） |

これらはデモと撮影用です。デモ中は画面に「DEMO」と出て、画面やキーに触れるとすべて止まり、手動で遊べるようになります。

## ライセンス

このリポジトリのコードは [MIT License](LICENSE) です。

ビルドしたアプリには CPython、QuickJS-ng、Wasmtime、FSharp.Core、OCaml のランタイム、Go のランタイムなどのサードパーティのソフトウェアが組み込まれます。一覧とライセンスは [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。ビルドしたアプリを配布するときは、それぞれのライセンスの条件に従ってください。
