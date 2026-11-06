#!/usr/bin/env bash
# 端末で自動操縦し、8 体のボスの画面（と巻き戻し中、被弾直後の画面）を撮って <出力先>/captures に取ってくる。
# 端末の画面を点け、ロックを解除しておくこと
set -euo pipefail
source "$(dirname "$0")/env.sh"
output="${1:?usage: capture_android.sh <output-dir>}"
package=com.unitysunday.polyglotbossrush
remote="/sdcard/Android/data/$package/files/captures"
"$ADB" install -r "$ROOT/Builds/Android/PolyglotBossRush.apk" >/dev/null
"$ADB" shell rm -rf "$remote"
"$ADB" shell am force-stop "$package"
"$ADB" logcat -c
# 撮影用の引数付きの起動は「最近使ったアプリ」に残さない（FLAG_ACTIVITY_EXCLUDE_FROM_RECENTS）。
# 残すと、あとで履歴から開いたときに同じ引数でデモが始まってしまう
"$ADB" shell am start -f 0x00800000 -n "$package/com.unity3d.player.UnityPlayerGameActivity" \
  -e unity "'-autoplay -rewind-at 4 -capture captures'" >/dev/null
for _ in $(seq 1 180); do
  "$ADB" logcat -d | grep -q -E '8-Tama\.png|FATAL|Fatal signal' && break
  sleep 1
done
sleep 2
"$ADB" logcat -d -s Unity:I | grep -E '\[BossRush\]|Exception' | sed -E 's#^.*Unity +: ##'
mkdir -p "$output"
"$ADB" pull "$remote" "$output" >/dev/null
"$ADB" shell am force-stop "$package"
