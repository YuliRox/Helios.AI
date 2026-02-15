package com.helios.lumirise.data.local

import androidx.room.TypeConverter
import com.helios.lumirise.domain.model.SyncStatus

class SyncStatusConverter {
    @TypeConverter
    fun fromSyncStatus(value: SyncStatus): String = value.name

    @TypeConverter
    fun toSyncStatus(value: String): SyncStatus =
        SyncStatus.entries.firstOrNull { it.name == value } ?: SyncStatus.ERROR
}
