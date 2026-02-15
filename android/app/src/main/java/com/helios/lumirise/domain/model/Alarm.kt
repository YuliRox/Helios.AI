package com.helios.lumirise.domain.model

data class Alarm(
    val id: String,
    val lightAlarmId: String?,
    val timestamp: Long,
    val enabled: Boolean,
    val label: String,
    val syncStatus: SyncStatus
)
