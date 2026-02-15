package com.helios.lumirise.data.local

import androidx.room.Database
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

@Database(
    entities = [AlarmEntity::class],
    version = 2,
    exportSchema = false
)
@TypeConverters(SyncStatusConverter::class)
abstract class LumiRiseDatabase : RoomDatabase() {
    abstract fun alarmDao(): AlarmDao

    companion object {
        val MIGRATION_1_2: Migration = object : Migration(1, 2) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE alarms ADD COLUMN daysOfWeekCsv TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE alarms ADD COLUMN timeOfDay TEXT NOT NULL DEFAULT ''")
            }
        }
    }
}
