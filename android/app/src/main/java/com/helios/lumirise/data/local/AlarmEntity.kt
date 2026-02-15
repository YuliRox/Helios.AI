package com.helios.lumirise.data.local

import androidx.room.Entity
import androidx.room.PrimaryKey
import com.helios.lumirise.domain.model.SyncStatus

@Entity(tableName = "alarms")
data class AlarmEntity(
    @PrimaryKey val id: String,
    val lightAlarmId: String?,
    val timestamp: Long,
    val enabled: Boolean,
    val label: String,
    val syncStatus: SyncStatus
)
