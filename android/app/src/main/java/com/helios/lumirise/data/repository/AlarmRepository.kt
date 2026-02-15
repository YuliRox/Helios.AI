package com.helios.lumirise.data.repository

import com.helios.lumirise.data.local.AlarmDao
import com.helios.lumirise.data.local.AlarmEntity
import com.helios.lumirise.data.prefs.SyncPreferencesStore
import com.helios.lumirise.domain.model.Alarm
import com.helios.lumirise.domain.sync.AlarmSyncManager
import com.helios.lumirise.domain.sync.ConflictResolutionStrategy
import com.helios.lumirise.domain.sync.HomeNetworkChecker
import com.helios.lumirise.domain.sync.SyncOutcome
import com.helios.lumirise.domain.sync.SystemAlarmGateway
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.map
import kotlin.math.abs

class AlarmRepository(
    private val alarmDao: AlarmDao,
    private val syncManager: AlarmSyncManager,
    private val homeNetworkChecker: HomeNetworkChecker,
    private val systemAlarmGateway: SystemAlarmGateway,
    private val preferencesStore: SyncPreferencesStore
) {
    private val _isAtHomeNetwork = MutableStateFlow(false)
    private val _currentNetworkSsid = MutableStateFlow<String?>(null)

    val isAtHomeNetwork: StateFlow<Boolean> = _isAtHomeNetwork.asStateFlow()
    val currentNetworkSsid: StateFlow<String?> = _currentNetworkSsid.asStateFlow()

    val alarms: Flow<List<Alarm>> = alarmDao.observeAll().map { entities ->
        entities.map { it.toDomain() }
    }

    val autoSyncEnabled: Flow<Boolean> = preferencesStore.autoSyncEnabled
    val apiBaseUrl: Flow<String> = preferencesStore.apiBaseUrl
    val homeNetworkSsid: Flow<String?> = preferencesStore.homeNetworkSsid

    suspend fun refreshHomePresence(): HomePresenceResult {
        val currentSsid = homeNetworkChecker.getCurrentWifiSsid()
        _currentNetworkSsid.value = currentSsid

        val configuredHomeSsid = preferencesStore.getHomeNetworkSsid()
        val ssidMatches = matchesConfiguredHomeSsid(configuredHomeSsid, currentSsid)
        val isApiReachable = if (ssidMatches) homeNetworkChecker.isApiReachable() else false
        val isAtHome = ssidMatches && isApiReachable
        _isAtHomeNetwork.value = isAtHome

        val previousHomeState = preferencesStore.getLastKnownHomeState(defaultValue = true)
        val currentSystemAlarm = systemAlarmGateway.getNextAlarmEpochMillis()

        var returnedHomeWithChangedSystemAlarm = false

        if (!isAtHome) {
            if (previousHomeState) {
                preferencesStore.setAwayBaselineSystemAlarmEpoch(currentSystemAlarm)
            }
        } else {
            if (!previousHomeState) {
                val awayBaseline = preferencesStore.getAwayBaselineSystemAlarmEpoch()
                returnedHomeWithChangedSystemAlarm = hasChangedBeyondThreshold(
                    awayBaseline,
                    currentSystemAlarm
                )
                preferencesStore.setAwayBaselineSystemAlarmEpoch(null)
            }
        }

        preferencesStore.setLastKnownHomeState(isAtHome)

        return HomePresenceResult(
            isAtHomeNetwork = isAtHome,
            returnedHomeWithChangedSystemAlarm = returnedHomeWithChangedSystemAlarm
        )
    }

    suspend fun createAlarm(timestamp: Long, label: String, enabled: Boolean = true) {
        syncManager.createAlarmFromApp(timestamp = timestamp, label = label, enabled = enabled)
    }

    suspend fun syncNow(
        strategy: ConflictResolutionStrategy = ConflictResolutionStrategy.NONE
    ): SyncOutcome = syncManager.sync(strategy)

    suspend fun setAutoSyncEnabled(enabled: Boolean) {
        preferencesStore.setAutoSyncEnabled(enabled)
    }

    suspend fun updateNetworkSettings(apiBaseUrl: String, homeSsid: String?) {
        preferencesStore.setApiBaseUrl(apiBaseUrl)
        preferencesStore.setHomeNetworkSsid(homeSsid)
    }

    suspend fun getCurrentNetworkSsid(): String? {
        val current = homeNetworkChecker.getCurrentWifiSsid()
        _currentNetworkSsid.value = current
        return current
    }

    private fun AlarmEntity.toDomain(): Alarm = Alarm(
        id = id,
        lightAlarmId = lightAlarmId,
        daysOfWeek = daysOfWeekCsv.split(',')
            .map { it.trim() }
            .filter { it.isNotEmpty() },
        timeOfDay = timeOfDay.ifBlank { null },
        timestamp = timestamp,
        enabled = enabled,
        label = label,
        syncStatus = syncStatus
    )

    private fun hasChangedBeyondThreshold(oldValue: Long?, newValue: Long?): Boolean {
        if (oldValue == null && newValue == null) {
            return false
        }
        if (oldValue == null || newValue == null) {
            return true
        }

        return abs(oldValue - newValue) > MAX_SYSTEM_ALARM_DRIFT_MS
    }

    private fun matchesConfiguredHomeSsid(configuredSsid: String?, currentSsid: String?): Boolean {
        if (configuredSsid.isNullOrBlank() || currentSsid.isNullOrBlank()) {
            return false
        }

        return configuredSsid.trim().equals(currentSsid.trim(), ignoreCase = false)
    }

    companion object {
        private const val MAX_SYSTEM_ALARM_DRIFT_MS = 60_000L
    }
}

data class HomePresenceResult(
    val isAtHomeNetwork: Boolean,
    val returnedHomeWithChangedSystemAlarm: Boolean
)
