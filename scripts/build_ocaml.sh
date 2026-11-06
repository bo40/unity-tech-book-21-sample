#!/usr/bin/env bash
# OCaml のボスを wasm_of_ocaml で WASI モジュールにし、macOS と Android 向けに事前コンパイルする。
# 事前コンパイルには build_rust.sh が作る precompile を使う
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$LANGS/ocaml"
mkdir -p build
ocamlfind ocamlc -no-check-prims -package js_of_ocaml -linkpkg boss.ml -o build/boss.byte
rm -rf build/boss.assets
wasm_of_ocaml compile --enable wasi --effects=cps unihub.wat build/boss.byte -o build/boss.js
cp build/boss.assets/code.wasm build/boss.wasm
precompile="$LANGS/rust/target/release/examples/precompile"
"$precompile" build/boss.wasm "$BOSS_RESOURCES/ocaml_macos.bytes" aarch64-apple-darwin
"$precompile" build/boss.wasm "$BOSS_RESOURCES/ocaml_android.bytes" aarch64-linux-android
echo "ocaml: ok"
