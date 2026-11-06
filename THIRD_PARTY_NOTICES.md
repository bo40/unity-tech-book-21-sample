# Third-party notices

このリポジトリ自身のコード（`Assets/PolyglotBossRush`、`langs/`、`scripts/`）は [MIT License](LICENSE) です。

リポジトリにはサードパーティのバイナリを含めていません。以下のソフトウェアは `scripts/build_*.sh` がビルド時に取得・コンパイルし、ビルドしたアプリ（`.app` や `.apk`）に組み込まれます。**ビルドしたアプリを配布するときは、各ライセンスの条件（著作権表示やライセンス文の同梱）に従ってください。**

## アプリに組み込まれるもの

| ソフトウェア | バージョン | ライセンス | 組み込まれ方 |
|---|---|---|---|
| [CPython](https://www.python.org/) | 3.14.10（Android） | [PSF License Agreement](https://docs.python.org/3/license.html)（Python-2.0） | Android: `libpython3.14.so` と標準ライブラリ（`python-stdlib.zip`）を同梱。macOS: Homebrew の Python を参照するだけで同梱しない |
| CPython に含まれるライブラリ | ― | OpenSSL（Apache-2.0）、SQLite（Public Domain）、libffi、zlib、bzip2、xz、expat、mpdecimal、HACL* など | 標準ライブラリの拡張モジュールに静的リンクされている。一覧と全文は `python-stdlib.zip` 内の `lib/python3.14/LICENSE.txt` |
| [QuickJS-ng](https://github.com/quickjs-ng/quickjs) | rquickjs 0.14.0 に同梱の版 | MIT（Copyright (c) 2017-2026 Fabrice Bellard、Charlie Gordon、Ben Noordhuis） | Rust の `libunihub` に静的リンク |
| [Wasmtime](https://github.com/bytecodealliance/wasmtime) | 50.0.0 | Apache-2.0 WITH LLVM-exception | Rust の `libunihub` に静的リンク（実行時部分のみ。Cranelift はホストでの事前コンパイルにだけ使う） |
| そのほかの Rust のクレート | 下の表 | 下の表 | Rust の `libunihub` に静的リンク |
| [FSharp.Core](https://github.com/dotnet/fsharp) | 10.1.403 | MIT（Copyright (c) Microsoft Corporation） | `FSharp.Core.dll` を同梱（IL2CPP で C++ に変換される） |
| [OCaml](https://ocaml.org/) の標準ライブラリ | 5.4.0 | LGPL-2.1 with OCaml LGPL Linking Exception | OCaml のボス（`.cwasm`）にコンパイルされて含まれる |
| [wasm_of_ocaml](https://github.com/ocsigen/js_of_ocaml) のランタイム | 6.5.0 | LGPL-2.1 with linking exception | OCaml のボス（`.cwasm`）に含まれる |
| [Go](https://go.dev/) のランタイムと標準ライブラリ | 1.25.6 | BSD-3-Clause（Copyright 2009 The Go Authors） | Go のボス（`.cwasm`）にコンパイルされて含まれる |
| Unity | 6000.6.7f1 | [Unity の利用規約](https://unity.com/legal) | エンジン本体 |

OCaml と wasm_of_ocaml のライセンスには「ライブラリとリンクした実行ファイルを、好きな条件で配布してよい」という例外が付いています。

Go の BSD-3-Clause では、バイナリを配布するときに著作権表示とライセンス文を添える必要があります。全文は Go のソースの `LICENSE`（<https://go.dev/LICENSE>）にあります。

## ビルドにだけ使うもの（アプリには含まれない）

| ソフトウェア | ライセンス |
|---|---|
| [TypeScript](https://github.com/microsoft/TypeScript) 7.0.2 | Apache-2.0 |
| [esbuild](https://github.com/evanw/esbuild) 0.28.2 | MIT |
| [Menhir](https://gitlab.inria.fr/fpottier/menhir) | GPL-2.0（生成器）。生成されたパーサは Tama のコンパイラ（ホスト用）に入るだけで、アプリには含まれない |
| [dune](https://github.com/ocaml/dune) | MIT |
| [Binaryen](https://github.com/WebAssembly/binaryen) | Apache-2.0 |
| LLVM / Clang、Android NDK | Apache-2.0 WITH LLVM-exception ほか |
| Cranelift（Wasmtime の `jit` feature） | Apache-2.0 WITH LLVM-exception |
| .NET SDK | MIT |

## Rust のクレート

`scripts/list_rust_licenses.sh` で作り直せます（`--no-default-features` で macOS と Android に組み込まれるもの）。

| クレート | バージョン | ライセンス |
|---|---|---|
| allocator-api2 | 0.2.21 | MIT OR Apache-2.0 |
| anyhow | 1.0.104 | MIT OR Apache-2.0 |
| async-trait | 0.1.92 | （proc-macro。バイナリには含まれない） |
| async-trait | 0.1.92 | MIT OR Apache-2.0 |
| bitflags | 2.13.2 | MIT OR Apache-2.0 |
| bumpalo | 3.20.3 | MIT OR Apache-2.0 |
| bytes | 1.12.1 | MIT |
| cfg-if | 1.0.5 | MIT OR Apache-2.0 |
| chacha20 | 0.10.2 | MIT OR Apache-2.0 |
| cobs | 0.3.0 | MIT OR Apache-2.0 |
| cranelift-bforest | 0.137.0 | Apache-2.0 WITH LLVM-exception |
| cranelift-bitset | 0.137.0 | Apache-2.0 WITH LLVM-exception |
| cranelift-entity | 0.137.0 | Apache-2.0 WITH LLVM-exception |
| crossbeam-deque | 0.8.8 | MIT OR Apache-2.0 |
| crossbeam-epoch | 0.9.21 | MIT OR Apache-2.0 |
| crossbeam-utils | 0.8.23 | MIT OR Apache-2.0 |
| displaydoc | 0.2.7 | （proc-macro。バイナリには含まれない） |
| displaydoc | 0.2.7 | MIT OR Apache-2.0 |
| either | 1.19.0 | MIT OR Apache-2.0 |
| encoding_rs | 0.8.42 | (Apache-2.0 OR MIT) AND BSD-3-Clause |
| equivalent | 1.0.3 | Apache-2.0 OR MIT |
| errno | 0.3.14 | MIT OR Apache-2.0 |
| foldhash | 0.2.0 | Zlib |
| form_urlencoded | 1.2.2 | MIT OR Apache-2.0 |
| futures | 0.3.34 | MIT OR Apache-2.0 |
| futures-channel | 0.3.34 | MIT OR Apache-2.0 |
| futures-core | 0.3.34 | MIT OR Apache-2.0 |
| futures-io | 0.3.34 | MIT OR Apache-2.0 |
| futures-sink | 0.3.34 | MIT OR Apache-2.0 |
| futures-task | 0.3.34 | MIT OR Apache-2.0 |
| futures-util | 0.3.34 | MIT OR Apache-2.0 |
| getrandom | 0.4.3 | MIT OR Apache-2.0 |
| gimli | 0.33.0 | MIT OR Apache-2.0 |
| hashbrown | 0.17.1 | MIT OR Apache-2.0 |
| heck | 0.5.0 | MIT OR Apache-2.0 |
| icu_collections | 2.4.0 | Unicode-3.0 |
| icu_locale_core | 2.4.0 | Unicode-3.0 |
| icu_normalizer | 2.4.0 | Unicode-3.0 |
| icu_normalizer_data | 2.4.0 | Unicode-3.0 |
| icu_properties | 2.4.0 | Unicode-3.0 |
| icu_properties_data | 2.4.0 | Unicode-3.0 |
| icu_provider | 2.4.0 | Unicode-3.0 |
| id-arena | 2.3.0 | MIT/Apache-2.0 |
| idna | 1.1.0 | MIT OR Apache-2.0 |
| idna_adapter | 1.2.2 | Apache-2.0 OR MIT |
| indexmap | 2.14.2 | Apache-2.0 OR MIT |
| itoa | 1.0.18 | MIT OR Apache-2.0 |
| leb128 | 0.2.7 | MIT OR Apache-2.0 |
| leb128fmt | 0.1.0 | MIT OR Apache-2.0 |
| libc | 0.2.191 | MIT OR Apache-2.0 |
| libm | 0.2.16 | MIT |
| linux-raw-sys | 0.12.1 | Apache-2.0 WITH LLVM-exception OR Apache-2.0 OR MIT |
| litemap | 0.8.3 | Unicode-3.0 |
| log | 0.4.34 | MIT OR Apache-2.0 |
| mach2 | 0.6.0 | BSD-2-Clause OR MIT OR Apache-2.0 |
| memchr | 2.8.3 | Unlicense OR MIT |
| mio | 1.2.4 | MIT |
| multiversion_no_op | 1.0.0 | Apache-2.0 OR MIT |
| object | 0.40.0 | Apache-2.0 OR MIT |
| once_cell | 1.21.4 | MIT OR Apache-2.0 |
| percent-encoding | 2.3.2 | MIT OR Apache-2.0 |
| pin-project-lite | 0.2.17 | Apache-2.0 OR MIT |
| postcard | 1.1.3 | MIT OR Apache-2.0 |
| potential_utf | 0.1.6 | Unicode-3.0 |
| proc-macro2 | 1.0.107 | MIT OR Apache-2.0 |
| pulley-interpreter | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| pulley-macros | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| quote | 1.0.47 | MIT OR Apache-2.0 |
| rand | 0.10.3 | MIT OR Apache-2.0 |
| rand_core | 0.10.1 | MIT OR Apache-2.0 |
| rayon | 1.12.0 | MIT OR Apache-2.0 |
| rayon-core | 1.13.0 | MIT OR Apache-2.0 |
| rquickjs | 0.14.0 | MIT |
| rquickjs-core | 0.14.0 | MIT |
| rquickjs-sys | 0.14.0 | MIT |
| rustix | 1.1.6 | Apache-2.0 WITH LLVM-exception OR Apache-2.0 OR MIT |
| rustix-linux-procfs | 0.1.1 | Apache-2.0 WITH LLVM-exception OR Apache-2.0 OR MIT |
| scopeguard | 1.2.0 | MIT OR Apache-2.0 |
| semver | 1.0.28 | MIT OR Apache-2.0 |
| serde | 1.0.229 | MIT OR Apache-2.0 |
| serde_core | 1.0.229 | MIT OR Apache-2.0 |
| serde_derive | 1.0.229 | （proc-macro。バイナリには含まれない） |
| serde_derive | 1.0.229 | MIT OR Apache-2.0 |
| serde_json | 1.0.154 | MIT OR Apache-2.0 |
| simdutf8 | 0.1.5 | MIT OR Apache-2.0 |
| smallvec | 1.16.4 | MIT OR Apache-2.0 |
| socket2 | 0.6.5 | MIT OR Apache-2.0 |
| stable_deref_trait | 1.2.1 | MIT OR Apache-2.0 |
| syn | 2.0.119 | MIT OR Apache-2.0 |
| syn | 3.0.9 | MIT OR Apache-2.0 |
| synstructure | 0.14.0 | MIT |
| target-lexicon | 0.13.5 | Apache-2.0 WITH LLVM-exception |
| thiserror | 1.0.69 | MIT OR Apache-2.0 |
| thiserror | 2.0.21 | MIT OR Apache-2.0 |
| thiserror-impl | 1.0.69 | MIT OR Apache-2.0 |
| thiserror-impl | 2.0.21 | （proc-macro。バイナリには含まれない） |
| thiserror-impl | 2.0.21 | MIT OR Apache-2.0 |
| tinystr | 0.8.5 | Unicode-3.0 |
| tokio | 1.54.1 | MIT |
| tracing | 0.1.44 | MIT |
| tracing-attributes | 0.1.31 | MIT |
| tracing-core | 0.1.36 | MIT |
| unicode-ident | 1.0.27 | (MIT OR Apache-2.0) AND Unicode-3.0 |
| url | 2.5.8 | MIT OR Apache-2.0 |
| utf8_iter | 1.0.4 | Apache-2.0 OR MIT |
| wasm-encoder | 0.261.0 | Apache-2.0 WITH LLVM-exception OR Apache-2.0 OR MIT |
| wasm-metadata | 0.261.0 | Apache-2.0 WITH LLVM-exception OR Apache-2.0 OR MIT |
| wasmparser | 0.261.0 | Apache-2.0 WITH LLVM-exception OR Apache-2.0 OR MIT |
| wasmtime | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wasmtime-environ | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wasmtime-internal-component-macro | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wasmtime-internal-component-util | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wasmtime-internal-core | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wasmtime-internal-fiber | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wasmtime-internal-jit-icache-coherence | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wasmtime-internal-unwinder | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wasmtime-internal-versioned-export-macros | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wasmtime-internal-wit-bindgen | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wasmtime-wasi | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wasmtime-wasi-io | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wast | 35.0.2 | Apache-2.0 WITH LLVM-exception |
| wiggle | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wiggle-generate | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wiggle-macro | 50.0.0 | Apache-2.0 WITH LLVM-exception |
| wit-component | 0.261.0 | Apache-2.0 WITH LLVM-exception OR Apache-2.0 OR MIT |
| wit-parser | 0.261.0 | Apache-2.0 WITH LLVM-exception OR Apache-2.0 OR MIT |
| witx | 0.9.1 | Apache-2.0 |
| writeable | 0.6.4 | Unicode-3.0 |
| yoke | 0.8.4 | Unicode-3.0 |
| yoke-derive | 0.8.4 | Unicode-3.0 |
| zerofrom | 0.1.8 | Unicode-3.0 |
| zerofrom-derive | 0.1.8 | Unicode-3.0 |
| zerotrie | 0.2.5 | Unicode-3.0 |
| zerovec | 0.11.9 | Unicode-3.0 |
| zerovec-derive | 0.11.7 | Unicode-3.0 |
| zmij | 1.0.23 | MIT |
