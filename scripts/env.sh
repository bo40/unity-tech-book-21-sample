# 各ビルドスクリプトが読み込む共通設定
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LANGS="$ROOT/langs"
GENERATED="$ROOT/Assets/PolyglotBossRush/Generated"
PLUGINS_MACOS="$GENERATED/Plugins/macOS"
PLUGINS_ANDROID="$GENERATED/Plugins/Android/arm64-v8a"
PLUGINS_MANAGED="$GENERATED/Plugins/Managed"
BOSS_RESOURCES="$GENERATED/Resources/Bosses"
STREAMING_ASSETS="$ROOT/Assets/StreamingAssets"
UNITY_VERSION="${UNITY_VERSION:-6000.6.7f1}"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"
export ANDROID_NDK_HOME="${ANDROID_NDK_HOME:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/PlaybackEngines/AndroidPlayer/NDK}"
NDK_CLANG="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/darwin-x86_64/bin/clang"
# Unity Editor は別の SDK の adb サーバーを見つけると終了させるので、Unity 同梱の adb を使う
ADB="${ADB:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb}"
# Command Line Tools の ld が MacOSX27 SDK の .tbd を読めない環境があるため、macOS 向けリンクは 26.5 SDK で行う
if [ -z "${SDKROOT:-}" ] && [ -d /Library/Developer/CommandLineTools/SDKs/MacOSX26.5.sdk ]; then
  export SDKROOT=/Library/Developer/CommandLineTools/SDKs/MacOSX26.5.sdk
fi
export PATH="/opt/homebrew/opt/rustup/bin:$HOME/.cargo/bin:$HOME/.unity/bin:$PATH"
eval "$(opam env 2>/dev/null)"
mkdir -p "$PLUGINS_MACOS" "$PLUGINS_ANDROID" "$PLUGINS_MANAGED" "$BOSS_RESOURCES" "$STREAMING_ASSETS"
