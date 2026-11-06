#!/usr/bin/env bash
# CPython を埋め込む pybridge をビルドする。macOS は Homebrew の Python、Android は python.org の公式版を使う
set -euo pipefail
source "$(dirname "$0")/env.sh"
PYTHON_VERSION=3.14.10
cd "$LANGS/python"
if [ ! -d android-prefix/prefix ]; then
  mkdir -p download android-prefix
  curl -sSfL -o "download/python-$PYTHON_VERSION-aarch64-linux-android.tar.gz" \
    "https://www.python.org/ftp/python/$PYTHON_VERSION/python-$PYTHON_VERSION-aarch64-linux-android.tar.gz"
  tar xzf "download/python-$PYTHON_VERSION-aarch64-linux-android.tar.gz" -C android-prefix
fi
mkdir -p build
PY_HOME=/opt/homebrew/opt/python@3.14/Frameworks/Python.framework/Versions/3.14
clang -O2 -shared -fPIC -o "$PLUGINS_MACOS/libpybridge.dylib" pybridge.c \
  $(python3.14-config --includes) -L"$PY_HOME/lib" -lpython3.14 -Wl,-rpath,"$PY_HOME/lib"
"$NDK_CLANG" --target=aarch64-linux-android24 -O2 -shared -fPIC -o "$PLUGINS_ANDROID/libpybridge.so" pybridge.c \
  -Iandroid-prefix/prefix/include/python3.14 -Landroid-prefix/prefix/lib -lpython3.14
cp android-prefix/prefix/lib/libpython3.14.so "$PLUGINS_ANDROID/"
# 標準ライブラリはテストや GUI 系を除いて zip にし、初回起動時に端末の内部ストレージへ展開する
rm -f build/python-stdlib.zip
(cd android-prefix/prefix && zip -qr ../../build/python-stdlib.zip lib/python3.14 \
  -x 'lib/python3.14/test/*' 'lib/python3.14/idlelib/*' 'lib/python3.14/ensurepip/*' \
     'lib/python3.14/tkinter/*' 'lib/python3.14/turtledemo/*' 'lib/python3.14/pydoc_data/*' \
     'lib/python3.14/lib-dynload/_ctypes_test*' 'lib/python3.14/lib-dynload/_test*' '*/__pycache__/*')
cp build/python-stdlib.zip "$STREAMING_ASSETS/"
cp boss.py "$BOSS_RESOURCES/python.txt"
echo "python: ok"
