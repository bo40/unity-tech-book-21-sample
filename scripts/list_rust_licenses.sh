#!/usr/bin/env bash
# アプリに組み込まれる Rust のクレートと、そのライセンスを Markdown の表で出力する。
# THIRD_PARTY_NOTICES.md の「Rust のクレート」の節を作り直すときに使う
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$LANGS/rust"
echo "| クレート | バージョン | ライセンス |"
echo "|---|---|---|"
for target in aarch64-linux-android aarch64-apple-darwin; do
  # proc-macro（ビルド時にだけ動き、バイナリには入らない）は -e normal でも出るので、ライセンス欄が空でも残す
  cargo tree --no-default-features -e normal --prefix none -f '{p}|{l}' --target "$target"
done | sed -E 's/ \(.*\)//' | grep -v '^unihub ' | sort -u | awk -F'|' '{ split($1, a, " v"); printf "| %s | %s | %s |\n", a[1], a[2], ($2 == "" ? "（proc-macro。バイナリには含まれない）" : $2) }'
