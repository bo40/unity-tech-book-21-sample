#!/usr/bin/env bash
# TypeScript のボスを型検査し、esbuild で 1 ファイルの JS にする
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$LANGS/typescript"
[ -d node_modules ] || npm install --silent
npx tsc --noEmit -p .
npx esbuild src/main.ts --bundle --format=iife --target=es2023 --outfile=dist/boss.js --log-level=warning
cp dist/boss.js "$BOSS_RESOURCES/typescript.txt"
echo "typescript: ok"
