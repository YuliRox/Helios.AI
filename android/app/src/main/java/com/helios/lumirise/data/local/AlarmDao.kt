package com.helios.lumirise.data.local

import androidx.room.Dao
import androidx.room.Query
import androidx.room.Upsert
import kotlinx.coroutines.flow.Flow

@Dao
interface AlarmDao {
    @Query("SELECT * FROM alarms ORDER BY timestamp ASC")
    fun observeAll(): Flow<List<AlarmEntity>>

    @Query("SELECT * FROM alarms ORDER BY timestamp ASC")
    suspend fun getAll(): List<AlarmEntity>

    @Query("SELECT * FROM alarms WHERE id = :id LIMIT 1")
    suspend fun findById(id: String): AlarmEntity?

    @Query("SELECT * FROM alarms WHERE lightAlarmId = :lightAlarmId LIMIT 1")
    suspend fun findByLightAlarmId(lightAlarmId: String): AlarmEntity?

    @Upsert
    suspend fun upsert(entity: AlarmEntity)

    @Upsert
    suspend fun upsertAll(entities: List<AlarmEntity>)

    @Query("DELETE FROM alarms")
    suspend fun clearAll()
}
