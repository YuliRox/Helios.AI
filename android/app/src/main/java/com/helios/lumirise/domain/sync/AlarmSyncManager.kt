package com.helios.lumirise.domain.sync

import com.helios.lumirise.api.AlarmResponseDto
import com.helios.lumirise.api.AlarmUpsertRequestDto
import com.helios.lumirise.api.LightAlarmApiProvider
import com.helios.lumirise.data.local.AlarmDao
import com.helios.lumirise.data.local.AlarmEntity
import com.helios.lumirise.domain.model.SyncStatus
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.time.DayOfWeek
import java.time.Instant
import java.time.LocalDateTime
import java.time.LocalTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.temporal.TemporalAdjusters
import java.util.Locale
import java.util.UUID
import kotlin.math.abs

class AlarmSyncManager(
    private val alarmDao: AlarmDao,
    private val lightAlarmApiProvider: LightAlarmApiProvider,
    private val systemAlarmGateway: SystemAlarmGateway,
    private val ioDispatcher: CoroutineDispatcher = Dispatchers.IO
) {
    suspend fun createAlarmFromApp(timestamp: Long, label: String, enabled: Boolean) = withContext(ioDispatcher) {
        val recurrence = recurrenceFromTimestamp(timestamp)
        val localEntity = AlarmEntity(
            id = UUID.randomUUID().toString(),
            lightAlarmId = null,
            daysOfWeekCsv = recurrence.days.joinToString(","),
            timeOfDay = recurrence.time,
            timestamp = timestamp,
            enabled = enabled,
            label = label,
            syncStatus = SyncStatus.LOCAL_ONLY
        )

        alarmDao.upsert(localEntity)
        systemAlarmGateway.scheduleAlarm(localEntity)

        val api = lightAlarmApiProvider.getApi()
        runCatching {
            api.createAlarm(localEntity.toUpsertRequest())
        }.onSuccess { remoteAlarm ->
            alarmDao.upsert(
                localEntity.copy(
                    lightAlarmId = remoteAlarm.id,
                    syncStatus = SyncStatus.SYNCED
                )
            )
        }.onFailure {
            alarmDao.upsert(localEntity.copy(syncStatus = SyncStatus.ERROR))
        }
    }

    suspend fun sync(
        strategy: ConflictResolutionStrategy = ConflictResolutionStrategy.NONE
    ): SyncOutcome = withContext(ioDispatcher) {
        val api = lightAlarmApiProvider.getApi()
        val remoteAlarms = runCatching { api.getAlarms() }.getOrElse {
            return@withContext SyncOutcome(hasDiscrepancy = false, success = false)
        }

        val localAlarms = alarmDao.getAll()
        val merged = mergeRemoteAndLocal(remoteAlarms, localAlarms)
        alarmDao.upsertAll(merged)

        val nextSystemAlarm = systemAlarmGateway.getNextAlarmEpochMillis()
        val nextRemoteAlarm = merged
            .asSequence()
            .filter { it.enabled && it.lightAlarmId != null }
            .minByOrNull { it.timestamp }
            ?.timestamp

        val hasDiscrepancy = isDiscrepancy(nextSystemAlarm, nextRemoteAlarm)

        if (hasDiscrepancy) {
            when (strategy) {
                ConflictResolutionStrategy.REMOTE_TO_SYSTEM -> {
                    nextRemoteAlarm?.let { ts ->
                        val alarm = merged.firstOrNull { it.timestamp == ts }
                        if (alarm != null) {
                            systemAlarmGateway.scheduleAlarm(alarm.copy(syncStatus = SyncStatus.SYNCED))
                            alarmDao.upsert(alarm.copy(syncStatus = SyncStatus.SYNCED))
                        }
                    }
                }

                ConflictResolutionStrategy.SYSTEM_TO_REMOTE -> {
                    nextSystemAlarm?.let { ts ->
                        val localAlarm = merged
                            .asSequence()
                            .filter { it.lightAlarmId == null }
                            .minByOrNull { abs(it.timestamp - ts) }
                        val label = localAlarm?.label ?: "Android Alarm"
                        val recurrence = recurrenceFromTimestamp(ts)
                        val localEntity = localAlarm?.copy(
                            daysOfWeekCsv = localAlarm.daysOfWeekCsv.ifBlank {
                                recurrence.days.joinToString(",")
                            },
                            timeOfDay = localAlarm.timeOfDay.ifBlank { recurrence.time },
                            timestamp = ts,
                            enabled = true,
                            label = label,
                            syncStatus = SyncStatus.LOCAL_ONLY
                        ) ?: AlarmEntity(
                            id = UUID.randomUUID().toString(),
                            lightAlarmId = null,
                            daysOfWeekCsv = recurrence.days.joinToString(","),
                            timeOfDay = recurrence.time,
                            timestamp = ts,
                            enabled = true,
                            label = label,
                            syncStatus = SyncStatus.LOCAL_ONLY
                        )

                        runCatching {
                            api.createAlarm(localEntity.toUpsertRequest())
                        }.onSuccess { remoteAlarm ->
                            alarmDao.upsert(
                                localEntity.copy(
                                    lightAlarmId = remoteAlarm.id,
                                    syncStatus = SyncStatus.SYNCED
                                )
                            )
                        }.onFailure {
                            alarmDao.upsert(localEntity.copy(syncStatus = SyncStatus.ERROR))
                        }
                    }
                }

                ConflictResolutionStrategy.NONE -> {
                    val outOfSyncAlarm = merged
                        .firstOrNull { it.lightAlarmId != null && it.enabled }
                    if (outOfSyncAlarm != null) {
                        alarmDao.upsert(outOfSyncAlarm.copy(syncStatus = SyncStatus.OUT_OF_SYNC))
                    }
                }
            }
        }

        SyncOutcome(hasDiscrepancy = hasDiscrepancy, success = true)
    }

    private suspend fun mergeRemoteAndLocal(
        remoteAlarms: List<AlarmResponseDto>,
        localAlarms: List<AlarmEntity>
    ): List<AlarmEntity> {
        val localByRemoteId = localAlarms
            .filter { !it.lightAlarmId.isNullOrBlank() }
            .associateBy { it.lightAlarmId }
            .toMutableMap()

        val merged = mutableListOf<AlarmEntity>()

        remoteAlarms.forEach { remote ->
            val matchedLocal = localByRemoteId.remove(remote.id)
            val remoteDays = parseDays(remote.daysOfWeek).map { it.toApiDayName() }
            val remoteTime = parseLocalTime(remote.time).format(TIME_FORMATTER)
            val remoteTimestamp = remote.nextOccurrenceEpochMillis()
            val status = if (matchedLocal == null) {
                SyncStatus.REMOTE_ONLY
            } else if (isDiscrepancy(matchedLocal.timestamp, remoteTimestamp)) {
                SyncStatus.OUT_OF_SYNC
            } else {
                SyncStatus.SYNCED
            }

            merged += AlarmEntity(
                id = matchedLocal?.id ?: UUID.randomUUID().toString(),
                lightAlarmId = remote.id,
                daysOfWeekCsv = remoteDays.joinToString(","),
                timeOfDay = remoteTime,
                timestamp = remoteTimestamp,
                enabled = remote.enabled,
                label = remote.name?.ifBlank { "Lichtwecker" } ?: "Lichtwecker",
                syncStatus = status
            )
        }

        val remainingLocal = localAlarms.filter { it.lightAlarmId == null }
        merged += remainingLocal

        localByRemoteId.values.forEach { staleLocal ->
            merged += staleLocal.copy(syncStatus = SyncStatus.LOCAL_ONLY)
        }

        return merged
    }

    private fun isDiscrepancy(left: Long?, right: Long?): Boolean {
        if (left == null && right == null) {
            return false
        }
        if (left == null || right == null) {
            return true
        }
        return abs(left - right) > MAX_ALLOWED_DRIFT_MS
    }

    private fun AlarmEntity.toUpsertRequest(): AlarmUpsertRequestDto {
        val storedDays = daysOfWeekCsv
            .split(',')
            .map { it.trim() }
            .filter { it.isNotEmpty() }
        val normalizedDays = parseDays(storedDays).map { it.toApiDayName() }
        val normalizedTime = runCatching {
            LocalTime.parse(timeOfDay, TIME_FORMATTER).format(TIME_FORMATTER)
        }.getOrNull()

        val fallback = recurrenceFromTimestamp(timestamp)

        return AlarmUpsertRequestDto(
            name = label,
            daysOfWeek = normalizedDays.ifEmpty { fallback.days },
            time = normalizedTime ?: fallback.time,
            enabled = enabled,
            rampMode = "default"
        )
    }

    private fun AlarmResponseDto.nextOccurrenceEpochMillis(): Long {
        val localTime = parseLocalTime(time)
        val days = parseDays(daysOfWeek)
        val now = LocalDateTime.now()

        val chosen = days.minOf { day ->
            val candidate = now.with(TemporalAdjusters.nextOrSame(day))
                .withHour(localTime.hour)
                .withMinute(localTime.minute)
                .withSecond(0)
                .withNano(0)

            if (candidate.isAfter(now)) {
                candidate
            } else {
                now.with(TemporalAdjusters.next(day))
                    .withHour(localTime.hour)
                    .withMinute(localTime.minute)
                    .withSecond(0)
                    .withNano(0)
            }
        }

        return chosen.atZone(ZoneId.systemDefault()).toInstant().toEpochMilli()
    }

    private fun parseLocalTime(rawValue: String?): LocalTime {
        if (rawValue.isNullOrBlank()) {
            return LocalTime.of(7, 0)
        }

        return runCatching {
            LocalTime.parse(rawValue, TIME_FORMATTER)
        }.getOrElse {
            LocalTime.of(7, 0)
        }
    }

    private fun parseDays(rawDays: List<String>?): List<DayOfWeek> {
        if (rawDays.isNullOrEmpty()) {
            return listOf(LocalDateTime.now().dayOfWeek)
        }

        val parsed = rawDays.mapNotNull { text ->
            runCatching {
                DayOfWeek.valueOf(text.uppercase(Locale.ROOT))
            }.getOrNull()
        }

        return parsed.ifEmpty { listOf(LocalDateTime.now().dayOfWeek) }
    }

    private fun DayOfWeek.toApiDayName(): String = name.lowercase().replaceFirstChar { it.titlecase() }

    private fun recurrenceFromTimestamp(timestamp: Long): Recurrence {
        val zoned = Instant.ofEpochMilli(timestamp).atZone(ZoneId.systemDefault())
        return Recurrence(
            days = listOf(zoned.dayOfWeek.toApiDayName()),
            time = zoned.toLocalTime().format(TIME_FORMATTER)
        )
    }

    companion object {
        private val TIME_FORMATTER = DateTimeFormatter.ofPattern("HH:mm")
        private const val MAX_ALLOWED_DRIFT_MS = 60_000L
    }
}

private data class Recurrence(
    val days: List<String>,
    val time: String
)

enum class ConflictResolutionStrategy {
    NONE,
    REMOTE_TO_SYSTEM,
    SYSTEM_TO_REMOTE
}

data class SyncOutcome(
    val hasDiscrepancy: Boolean,
    val success: Boolean
)
