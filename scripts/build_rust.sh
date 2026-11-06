#!/usr/bin/env bash
# Rust の多言語ハブ（弾幕エンジン、重力井戸のボス、QuickJS、wasmtime）をビルドする。
# Unity に載せるライブラリは Cranelift なし（--no-default-features）で、事前コンパイル済みの .cwasm だけを読む。
# ホスト用の precompile（OCaml の事前コンパイルに使う）は JIT ありでビルドする
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$LANGS/rust"
cargo build --release --example precompile
cargo build --release --lib --no-default-features
# Android 向けは rquickjs の bindgen が libclang を使う
LIBCLANG_PATH="${LIBCLANG_PATH:-/Library/Developer/CommandLineTools/usr/lib}" \
  cargo ndk -t arm64-v8a -P 24 build --release --lib --no-default-features
cp target/release/libunihub.dylib "$PLUGINS_MACOS/"
cp target/aarch64-linux-android/release/libunihub.so "$PLUGINS_ANDROID/"
echo "rust: ok"
