# Install LumiRise Android App

This guide shows how to build the Android app and install it on a physical Android phone.

## 1. Prerequisites (PC)

Install these tools once:

1. Android Studio (latest stable).
2. Android SDK Platform 35.
3. Android SDK Build-Tools 35.x.
4. Android SDK Platform-Tools (`adb`).
5. JDK 17 (Android Studio usually installs a compatible JDK automatically).

Verify from terminal:

```bash
java -version
adb version
```

## 2. Build the APK

From repo root:

```bash
./android/build-debug.sh
```

APK output:

```text
android/app/build/outputs/apk/debug/app-debug.apk
```

## 3. Install on Phone (first time, never sideloaded before)

Choose one method.

### Method A (recommended): USB + `adb install`

1. On phone, open `Settings -> About phone`.
2. Tap `Build number` 7 times to enable Developer Options.
3. Open `Settings -> Developer options`.
4. Enable `USB debugging`.
5. Connect phone to PC by USB.
6. Confirm the "Allow USB debugging" prompt on the phone.
7. Run:

```bash
./android/build-debug.sh --install
```

If already built:

```bash
adb install -r android/app/build/outputs/apk/debug/app-debug.apk
```

### Method B: Copy APK and install manually on phone

1. Build APK (`./android/build-debug.sh`).
2. Copy `app-debug.apk` to phone (USB file transfer, cloud drive, or messaging app).
3. On phone, open the APK file.
4. Android will ask to allow installs from that source (Files app / browser / messenger).
5. Enable `Allow from this source`, then install.

Note: This "Unknown apps" permission is per app/source on modern Android.

## 4. First Launch Requirements in LumiRise

On first app start, allow runtime permissions when prompted:

1. Notifications (for sync/conflict notifications).
2. Location (needed on Android 10+ to read Wi-Fi SSID for home-network detection).

If denied, features like SSID-based home detection and notifications will not work correctly.

## 5. Configure LumiRise to Work on a Physical Phone

`http://10.0.2.2:8080/` only works in the Android emulator, not on a real phone.

For a phone:

1. Start backend on your local network (example from repo root):

```bash
docker compose up -d
```

2. Find your computer's LAN IP (example: `192.168.1.50`).
3. In app settings, set API URI to:

```text
http://192.168.1.50:8080/
```

4. Set your home SSID in app settings.
5. You can use the button `Use current network as home network`.

## 6. Troubleshooting

`adb: command not found`

- Install Android SDK Platform-Tools and add it to `PATH`.

`INSTALL_FAILED_VERSION_DOWNGRADE`

- Uninstall old app first:

```bash
adb uninstall com.helios.lumirise
```

App opens but sync is disabled

- Check phone is on home Wi-Fi.
- Ensure location permission is granted and location services are enabled.
- Verify API URI uses your LAN IP and backend is running.
