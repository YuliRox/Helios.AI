package com.helios.lumirise.domain.sync

import android.app.AlarmManager
import android.app.PendingIntent
import android.os.Build
import android.content.Context
import android.content.Intent
import com.helios.lumirise.data.local.AlarmEntity
import com.helios.lumirise.workers.SystemAlarmReceiver

class AndroidSystemAlarmGateway(context: Context) : SystemAlarmGateway {
    private val appContext = context.applicationContext
    private val alarmManager = appContext.getSystemService(Context.ALARM_SERVICE) as AlarmManager
    private val storage = appContext.getSharedPreferences(STORAGE_NAME, Context.MODE_PRIVATE)

    override fun getScheduledAlarmEpochMillisByAlarmId(): Map<String, Long> {
        val now = System.currentTimeMillis()
        val active = storage.all
            .asSequence()
            .filter { (key, value) -> key.startsWith(ALARM_KEY_PREFIX) && value is Long }
            .map { (key, value) -> key.removePrefix(ALARM_KEY_PREFIX) to (value as Long) }
            .toList()

        val staleAlarmIds = active
            .filter { (_, triggerAt) -> triggerAt < now - STALE_TOLERANCE_MS }
            .map { (alarmId, _) -> alarmId }
        if (staleAlarmIds.isNotEmpty()) {
            val editor = storage.edit()
            staleAlarmIds.forEach { alarmId ->
                editor.remove(storageKey(alarmId))
                editor.remove(requestCodeKey(alarmId))
            }
            editor.apply()
        }

        return active
            .asSequence()
            .filter { (_, triggerAt) -> triggerAt >= now - STALE_TOLERANCE_MS }
            .associate { (alarmId, triggerAt) -> alarmId to triggerAt }
    }

    override fun getScheduledAlarmEpochMillis(): Set<Long> {
        return getScheduledAlarmEpochMillisByAlarmId().values.toSet()
    }

    override fun getNextAlarmEpochMillis(): Long? {
        return getScheduledAlarmEpochMillisByAlarmId().values.minOrNull()
    }

    override fun scheduleAlarm(alarm: AlarmEntity) {
        if (!alarm.enabled) {
            cancelAlarm(alarm.id)
            return
        }

        if (!canScheduleExactAlarms()) {
            return
        }

        val showIntent = createPendingIntent(alarm.id, alarm.label)
        val alarmClockInfo = AlarmManager.AlarmClockInfo(alarm.timestamp, showIntent)
        alarmManager.setAlarmClock(alarmClockInfo, showIntent)
        storage.edit().putLong(storageKey(alarm.id), alarm.timestamp).apply()
    }

    override fun cancelAlarm(alarmId: String) {
        val requestCode = getRequestCodeIfExists(alarmId)
        if (requestCode != null) {
            val intent = Intent(appContext, SystemAlarmReceiver::class.java)
            val pendingIntent = PendingIntent.getBroadcast(
                appContext,
                requestCode,
                intent,
                PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_NO_CREATE
            )
            if (pendingIntent != null) {
                alarmManager.cancel(pendingIntent)
                pendingIntent.cancel()
            }
        }
        storage.edit()
            .remove(storageKey(alarmId))
            .remove(requestCodeKey(alarmId))
            .apply()
    }

    private fun createPendingIntent(alarmId: String, label: String): PendingIntent {
        val requestCode = getOrCreateRequestCode(alarmId)
        val intent = Intent(appContext, SystemAlarmReceiver::class.java)
            .putExtra(SystemAlarmReceiver.EXTRA_ALARM_LABEL, label)

        return PendingIntent.getBroadcast(
            appContext,
            requestCode,
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
    }

    private fun storageKey(alarmId: String): String = "$ALARM_KEY_PREFIX$alarmId"
    private fun requestCodeKey(alarmId: String): String = "$REQUEST_CODE_KEY_PREFIX$alarmId"

    private fun getRequestCodeIfExists(alarmId: String): Int? {
        val stored = storage.getInt(requestCodeKey(alarmId), REQUEST_CODE_MISSING)
        return if (stored == REQUEST_CODE_MISSING) null else stored
    }

    private fun getOrCreateRequestCode(alarmId: String): Int {
        getRequestCodeIfExists(alarmId)?.let { return it }

        val next = storage.getInt(REQUEST_CODE_COUNTER_KEY, 0) + 1
        storage.edit()
            .putInt(REQUEST_CODE_COUNTER_KEY, next)
            .putInt(requestCodeKey(alarmId), next)
            .apply()
        return next
    }

    private fun canScheduleExactAlarms(): Boolean {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.S) {
            return true
        }
        return alarmManager.canScheduleExactAlarms()
    }

    companion object {
        private const val STORAGE_NAME = "lumirise_system_alarms"
        private const val ALARM_KEY_PREFIX = "alarm_"
        private const val REQUEST_CODE_KEY_PREFIX = "request_code_"
        private const val REQUEST_CODE_COUNTER_KEY = "request_code_counter"
        private const val REQUEST_CODE_MISSING = -1
        private const val STALE_TOLERANCE_MS = 60_000L
    }
}
