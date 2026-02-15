#!/usr/bin/env bash
set -euo pipefail

ANDROID_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APK_PATH="$ANDROID_DIR/app/build/outputs/apk/debug/app-debug.apk"
export GRADLE_USER_HOME="${GRADLE_USER_HOME:-$ANDROID_DIR/.gradle}"

cd "$ANDROID_DIR"

echo "Building debug APK..."
TMP_GRADLEW="$(mktemp "$ANDROID_DIR/.gradlew-unix.XXXXXX")"
trap 'rm -f "$TMP_GRADLEW"' EXIT
tr -d '\r' < ./gradlew > "$TMP_GRADLEW"
chmod +x "$TMP_GRADLEW"
"$TMP_GRADLEW" :app:assembleDebug

if [[ ! -f "$APK_PATH" ]]; then
  echo "Build finished, but APK not found at: $APK_PATH" >&2
  exit 1
fi

echo "APK built successfully:"
echo "  $APK_PATH"

if [[ "${1:-}" == "--install" ]]; then
  if ! command -v adb >/dev/null 2>&1; then
    echo "adb is not installed or not in PATH. Install Android SDK Platform-Tools." >&2
    exit 1
  fi

  echo "Installing APK via adb..."
  adb start-server >/dev/null
  adb install -r "$APK_PATH"
  echo "Install completed."
fi
