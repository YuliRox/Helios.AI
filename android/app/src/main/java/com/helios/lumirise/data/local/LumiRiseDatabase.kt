package com.helios.lumirise.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters

@Database(
    entities = [AlarmEntity::class],
    version = 1,
    exportSchema = false
)
@TypeConverters(SyncStatusConverter::class)
abstract class LumiRiseDatabase : RoomDatabase() {
    abstract fun alarmDao(): AlarmDao
}
