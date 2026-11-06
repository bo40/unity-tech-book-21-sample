#!/usr/bin/env bash
# Go のボスを WASI（wasip1）の Wasm にし、macOS と Android 向けに事前コンパイルする。
# 事前コンパイルには build_rust.sh が作る precompile を使う
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$LANGS/go"
mkdir -p build
GOOS=wasip1 GOARCH=wasm go build -trimpath -o build/boss.wasm .
precompile="$LANGS/rust/target/release/examples/precompile"
"$precompile" build/boss.wasm "$BOSS_RESOURCES/go_macos.bytes" aarch64-apple-darwin
"$precompile" build/boss.wasm "$BOSS_RESOURCES/go_android.bytes" aarch64-linux-android
echo "go: ok"
