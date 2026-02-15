package com.helios.lumirise.ui

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.helios.lumirise.data.repository.AlarmRepository
import com.helios.lumirise.domain.model.Alarm
import com.helios.lumirise.domain.model.SyncStatus
import com.helios.lumirise.domain.sync.ConflictResolutionStrategy
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.net.URI
import java.time.LocalDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter

class AlarmViewModel(
    private val repository: AlarmRepository
) : ViewModel() {
    private val _uiState = MutableStateFlow(AlarmUiState())
    val uiState: StateFlow<AlarmUiState> = _uiState.asStateFlow()

    init {
        observeAlarms()
        observeAutoSyncSetting()
        observeNetworkSettings()
        observeCurrentNetworkSsid()
        observeHomePresence()
        refreshHomePresenceAndInitialSync()
    }

    fun syncNow(strategy: ConflictResolutionStrategy = ConflictResolutionStrategy.NONE) {
        viewModelScope.launch {
            if (!ensureAtHome(userInitiated = true)) {
                return@launch
            }

            performSync(strategy)
        }
    }

    fun onAutoSyncChanged(enabled: Boolean) {
        viewModelScope.launch {
            if (!ensureAtHome(userInitiated = true)) {
                return@launch
            }

            repository.setAutoSyncEnabled(enabled)
            if (enabled) {
                performSync(ConflictResolutionStrategy.REMOTE_TO_SYSTEM)
            }
        }
    }

    fun createAlarm(label: String, timeInput: String) {
        viewModelScope.launch {
            if (!ensureAtHome(userInitiated = true)) {
                return@launch
            }

            val timestamp = runCatching {
                parseNextOccurrenceTimestamp(timeInput)
            }.getOrElse {
                emitError("Zeit muss im Format HH:mm sein.")
                return@launch
            }

            repository.createAlarm(timestamp = timestamp, label = label.ifBlank { "LumiRise Alarm" })
            performSync(ConflictResolutionStrategy.REMOTE_TO_SYSTEM)
        }
    }

    fun saveNetworkSettings(apiBaseUrl: String, homeSsid: String) {
        viewModelScope.launch {
            val normalizedUrl = normalizeApiBaseUrl(apiBaseUrl)
            if (normalizedUrl == null) {
                emitError("API URI muss mit http:// oder https:// beginnen.")
                return@launch
            }

            repository.updateNetworkSettings(
                apiBaseUrl = normalizedUrl,
                homeSsid = homeSsid.trim().ifBlank { null }
            )

            val presence = repository.refreshHomePresence()
            _uiState.update { state -> state.copy(isAtHomeNetwork = presence.isAtHomeNetwork) }

            if (presence.isAtHomeNetwork) {
                performSync(ConflictResolutionStrategy.NONE)
            }
        }
    }

    fun refreshCurrentNetworkSsid() {
        viewModelScope.launch {
            repository.getCurrentNetworkSsid()
        }
    }

    fun refreshHomePresence() {
        viewModelScope.launch {
            val presence = repository.refreshHomePresence()
            _uiState.update { state -> state.copy(isAtHomeNetwork = presence.isAtHomeNetwork) }
        }
    }

    fun applyRemoteToSystemResolution() {
        syncNow(ConflictResolutionStrategy.REMOTE_TO_SYSTEM)
    }

    fun applySystemToRemoteResolution() {
        syncNow(ConflictResolutionStrategy.SYSTEM_TO_REMOTE)
    }

    private fun observeAlarms() {
        viewModelScope.launch {
            repository.alarms.collect { alarms ->
                _uiState.update { state ->
                    state.copy(
                        alarms = alarms,
                        showDiscrepancyWarning = state.isAtHomeNetwork && hasSyncProblem(alarms)
                    )
                }
            }
        }
    }

    private fun observeAutoSyncSetting() {
        viewModelScope.launch {
            repository.autoSyncEnabled.collect { enabled ->
                _uiState.update { state -> state.copy(autoSyncEnabled = enabled) }
            }
        }
    }

    private fun observeNetworkSettings() {
        viewModelScope.launch {
            repository.apiBaseUrl.collect { value ->
                _uiState.update { state -> state.copy(apiBaseUrl = value) }
            }
        }

        viewModelScope.launch {
            repository.homeNetworkSsid.collect { value ->
                _uiState.update { state -> state.copy(homeNetworkSsid = value.orEmpty()) }
            }
        }
    }

    private fun observeCurrentNetworkSsid() {
        viewModelScope.launch {
            repository.currentNetworkSsid.collect { ssid ->
                _uiState.update { state -> state.copy(currentNetworkSsid = ssid) }
            }
        }
    }

    private fun observeHomePresence() {
        viewModelScope.launch {
            repository.isAtHomeNetwork.collect { isAtHome ->
                _uiState.update { state ->
                    state.copy(
                        isAtHomeNetwork = isAtHome,
                        showDiscrepancyWarning = isAtHome && hasSyncProblem(state.alarms)
                    )
                }
            }
        }
    }

    private fun refreshHomePresenceAndInitialSync() {
        viewModelScope.launch {
            val presence = repository.refreshHomePresence()
            _uiState.update { state -> state.copy(isAtHomeNetwork = presence.isAtHomeNetwork) }

            if (presence.isAtHomeNetwork) {
                performSync(ConflictResolutionStrategy.NONE)
            }
        }
    }

    private suspend fun ensureAtHome(userInitiated: Boolean): Boolean {
        val presence = repository.refreshHomePresence()
        _uiState.update { state -> state.copy(isAtHomeNetwork = presence.isAtHomeNetwork) }

        if (!presence.isAtHomeNetwork) {
            if (userInitiated) {
                emitError("Nicht im Heimnetz: Lichtwecker-Sync ist deaktiviert.")
            }
            return false
        }

        return true
    }

    private suspend fun performSync(strategy: ConflictResolutionStrategy) {
        _uiState.update { it.copy(isSyncing = true, errorMessage = null) }
        val result = repository.syncNow(strategy)

        _uiState.update { state ->
            state.copy(
                isSyncing = false,
                showDiscrepancyWarning = state.isAtHomeNetwork && result.hasDiscrepancy,
                errorMessage = if (result.success) null else "Synchronisierung fehlgeschlagen.",
                errorEventId = if (result.success) state.errorEventId else System.nanoTime()
            )
        }
    }

    private fun emitError(message: String) {
        _uiState.update { state ->
            state.copy(
                errorMessage = message,
                errorEventId = System.nanoTime()
            )
        }
    }

    private fun hasSyncProblem(alarms: List<Alarm>): Boolean = alarms.any { alarm ->
        alarm.syncStatus == SyncStatus.OUT_OF_SYNC || alarm.syncStatus == SyncStatus.ERROR
    }

    private fun parseNextOccurrenceTimestamp(timeInput: String): Long {
        val parsedTime = LocalDateTime.now().toLocalDate().atTime(
            java.time.LocalTime.parse(timeInput, DateTimeFormatter.ofPattern("HH:mm"))
        )

        val now = LocalDateTime.now()
        val nextOccurrence = if (parsedTime.isAfter(now)) parsedTime else parsedTime.plusDays(1)

        return nextOccurrence
            .atZone(ZoneId.systemDefault())
            .toInstant()
            .toEpochMilli()
    }

    private fun normalizeApiBaseUrl(rawValue: String): String? {
        val trimmed = rawValue.trim()
        if (trimmed.isEmpty()) {
            return null
        }

        return runCatching {
            val normalized = if (trimmed.endsWith('/')) trimmed else "$trimmed/"
            val uri = URI(normalized)
            if (uri.scheme !in setOf("http", "https") || uri.host.isNullOrBlank()) {
                null
            } else {
                normalized
            }
        }.getOrNull()
    }

    class Factory(private val repository: AlarmRepository) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            return AlarmViewModel(repository) as T
        }
    }
}
