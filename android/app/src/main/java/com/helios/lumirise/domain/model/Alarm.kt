package com.helios.lumirise.domain.model

data class Alarm(
    val id: String,
    val lightAlarmId: String?,
    val daysOfWeek: List<String> = emptyList(),
    val timeOfDay: String? = null,
    val timestamp: Long,
    val enabled: Boolean,
    val label: String,
    val syncStatus: SyncStatus
)
