Erstelle eine Android-App (Kotlin) für Lichtwecker-Synchronisation mit folgenden Anforderungen:

KERNFUNKTION:
- Synchronisiere Android-System-Wecker bidirektional mit Lichtwecker über REST-API
- User kann Wecker in der App setzen → beide Systeme werden gleichzeitig aktualisiert
- Background-Monitoring erkennt manuelle Änderungen an Android-Weckern und warnt bei Diskrepanz

ARCHITEKTUR:
- MVVM mit Jetpack Compose UI
- Retrofit für REST-API Kommunikation (OpenAPI JSON liegt vor: samples/swagger.json)
- Room Database als lokale Cache/Source of Truth
- WorkManager für periodische Sync-Checks (alle 15 Min)
- AlarmManager API für Android-System-Wecker Integration

HAUPTKOMPONENTEN:
1. AlarmSyncManager: Zentrale Sync-Logik zwischen Android/REST-API/Local DB
2. AlarmSyncWorker: Background-Service prüft getNextAlarmClock() und vergleicht mit Lichtwecker
3. LightAlarmApi: Retrofit Interface (aus OpenAPI generieren)
4. AlarmEntity: Room DB mit Feldern: id, lightAlarmId, timestamp, enabled, label, syncStatus
5. AlarmViewModel: UI State Management

UI FEATURES:
- Liste aller Wecker mit Sync-Status-Indicator
- Warning-Banner bei Diskrepanz mit "Jetzt synchronisieren" Button
- FAB zum Erstellen neuer Wecker (setzt beide Systeme gleichzeitig)
- Toggle für Auto-Sync Präferenz

SYNC-STRATEGIE:
- Primary: User setzt Wecker in App → beide Systeme sofort aktualisiert
- Fallback: Polling mit getNextAlarmClock() erkennt manuelle Änderungen → Notification
- Konflikte: User-Entscheidung via Notification mit Aktionen

DEPENDENCIES:
Retrofit, Room, WorkManager, Coroutines, Compose, OpenAPI Generator Plugin

Erstelle Projektstruktur mit packages: ui, data, domain, api, workers
