#!/usr/bin/env bash
# F# のボスをビルドし、FSharp.Core と一緒に Unity に置く
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$LANGS/fsharp/BossFSharp"
dotnet build -c Release -o "$LANGS/fsharp/out" >/dev/null
cp "$LANGS/fsharp/out/BossFSharp.dll" "$LANGS/fsharp/out/FSharp.Core.dll" "$PLUGINS_MANAGED/"
echo "fsharp: ok"
