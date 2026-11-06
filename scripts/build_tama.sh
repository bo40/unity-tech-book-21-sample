#!/usr/bin/env bash
# Tama のボスを LLVM IR にコンパイルし、ランタイムと一緒に共有ライブラリにする
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$LANGS/tama"
mkdir -p build
dune build
dune exec --no-print-directory compiler/tamac.exe -- examples/ore.tama -o build/ore.ll
# IR にはターゲットを書いていないので、clang の --target で決める
clang -Wno-override-module -O2 -shared -fPIC build/ore.ll runtime/tama_rt.c -o "$PLUGINS_MACOS/libtama_ore.dylib"
"$NDK_CLANG" -Wno-override-module --target=aarch64-linux-android24 -O2 -shared -fPIC \
  build/ore.ll runtime/tama_rt.c -lm -o "$PLUGINS_ANDROID/libtama_ore.so"
echo "tama: ok"
