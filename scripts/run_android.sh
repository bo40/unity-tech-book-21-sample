#!/usr/bin/env bash
# Builds/Android/PolyglotBossRush.apk を接続中の端末に入れて起動し、ログを流す。
# 引数はそのまま Unity のコマンドライン引数になる（例: run_android.sh -autoplay -boss 3）
set -euo pipefail
source "$(dirname "$0")/env.sh"
package=com.unitysunday.polyglotbossrush
"$ADB" install -r "$ROOT/Builds/Android/PolyglotBossRush.apk" >/dev/null
"$ADB" shell am force-stop "$package"
"$ADB" logcat -c
"$ADB" shell am start -n "$package/com.unity3d.player.UnityPlayerGameActivity" -e unity "'$*'" >/dev/null
"$ADB" logcat -s Unity:I | grep --line-buffered -E '\[BossRush\]|Exception'
