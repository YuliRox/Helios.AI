package com.helios.lumirise.ui

import com.helios.lumirise.domain.model.Alarm

data class AlarmUiState(
    val alarms: List<Alarm> = emptyList(),
    val isAtHomeNetwork: Boolean = true,
    val apiBaseUrl: String = "",
    val homeNetworkSsid: String = "",
    val currentNetworkSsid: String? = null,
    val autoSyncEnabled: Boolean = true,
    val isSyncing: Boolean = false,
    val showDiscrepancyWarning: Boolean = false,
    val errorMessage: String? = null
)
