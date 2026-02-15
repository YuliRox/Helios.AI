package com.helios.lumirise.data.local

import androidx.room.Entity
import androidx.room.PrimaryKey
import com.helios.lumirise.domain.model.SyncStatus

@Entity(tableName = "alarms")
data class AlarmEntity(
    @PrimaryKey val id: String,
    val lightAlarmId: String?,
    // Comma-separated weekday names (e.g. "Monday,Wednesday").
    val daysOfWeekCsv: String = "",
    // Time in HH:mm format from remote contract.
    val timeOfDay: String = "",
    val timestamp: Long,
    val enabled: Boolean,
    val label: String,
    val syncStatus: SyncStatus
)
