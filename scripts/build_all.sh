#!/usr/bin/env bash
# すべての言語のボスをビルドして Assets/PolyglotBossRush/Generated に配置する
set -euo pipefail
dir="$(dirname "$0")"
"$dir/build_rust.sh"        # OCaml と Go の事前コンパイルが precompile を使うので最初に
"$dir/build_fsharp.sh"
"$dir/build_typescript.sh"
"$dir/build_python.sh"
"$dir/build_ocaml.sh"
"$dir/build_go.sh"
"$dir/build_tama.sh"
