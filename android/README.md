# LumiRise Android Sync App

Android app scaffold for bidirectional synchronization between Android system alarms and LumiRise light alarms.

## Highlights

- MVVM + Jetpack Compose UI
- Room as local source of truth (`AlarmEntity`)
- Retrofit API client (`LightAlarmApi`) aligned with `samples/swagger.json`
- OpenAPI Generator plugin configured in `app/build.gradle.kts`
- WorkManager periodic sync (`AlarmSyncWorker`) every 15 minutes
- AlarmManager integration (`AndroidSystemAlarmGateway`)
- Conflict notification actions for remote/local resolution
- Home-network gating:
  - Light alarm actions are enabled only when the app can reach LumiRise API via Wi-Fi/Ethernet.
  - On return home, if system alarm changed while away, a notification asks whether to sync now or adjust first.
  - User settings dialog supports editing:
    - Lichtwecker API URI
    - Home network SSID
    - "Use current network as home network" helper button

## Structure

- `app/src/main/java/com/helios/lumirise/ui`
- `app/src/main/java/com/helios/lumirise/data`
- `app/src/main/java/com/helios/lumirise/domain`
- `app/src/main/java/com/helios/lumirise/api`
- `app/src/main/java/com/helios/lumirise/workers`

## Run

Open the `android/` directory in Android Studio and run the `app` module.

The API base URL defaults to emulator localhost (`http://10.0.2.2:8080/`) via `BuildConfig.LIGHT_ALARM_BASE_URL`.
