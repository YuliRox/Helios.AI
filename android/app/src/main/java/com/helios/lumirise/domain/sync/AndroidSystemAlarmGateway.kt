package com.helios.lumirise.domain.sync

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import com.helios.lumirise.data.local.AlarmEntity
import com.helios.lumirise.workers.SystemAlarmReceiver

class AndroidSystemAlarmGateway(context: Context) : SystemAlarmGateway {
    private val appContext = context.applicationContext
    private val alarmManager = appContext.getSystemService(Context.ALARM_SERVICE) as AlarmManager
    private val storage = appContext.getSharedPreferences(STORAGE_NAME, Context.MODE_PRIVATE)

    override fun getNextAlarmEpochMillis(): Long? {
        val now = System.currentTimeMillis()
        val active = storage.all
            .asSequence()
            .filter { (key, value) -> key.startsWith(ALARM_KEY_PREFIX) && value is Long }
            .map { (key, value) -> key to (value as Long) }
            .toList()

        val staleKeys = active
            .filter { (_, triggerAt) -> triggerAt < now - STALE_TOLERANCE_MS }
            .map { (key, _) -> key }
        if (staleKeys.isNotEmpty()) {
            val editor = storage.edit()
            staleKeys.forEach(editor::remove)
            editor.apply()
        }

        return active
            .asSequence()
            .map { (_, triggerAt) -> triggerAt }
            .filter { it >= now - STALE_TOLERANCE_MS }
            .minOrNull()
    }

    override fun scheduleAlarm(alarm: AlarmEntity) {
        if (!alarm.enabled) {
            cancelAlarm(alarm.id)
            return
        }

        val showIntent = createPendingIntent(alarm.id, alarm.label)
        val alarmClockInfo = AlarmManager.AlarmClockInfo(alarm.timestamp, showIntent)
        alarmManager.setAlarmClock(alarmClockInfo, showIntent)
        storage.edit().putLong(storageKey(alarm.id), alarm.timestamp).apply()
    }

    override fun cancelAlarm(alarmId: String) {
        val intent = Intent(appContext, SystemAlarmReceiver::class.java)
        val pendingIntent = PendingIntent.getBroadcast(
            appContext,
            alarmId.hashCode(),
            intent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_NO_CREATE
        )
        if (pendingIntent != null) {
            alarmManager.cancel(pendingIntent)
            pendingIntent.cancel()
        }
        storage.edit().remove(storageKey(alarmId)).apply()
    }

    private fun createPendingIntent(alarmId: String, label: String): PendingIntent {
        val intent = Intent(appContext, SystemAlarmReceiver::class.java)
            .putExtra(SystemAlarmReceiver.EXTRA_ALARM_LABEL, label)

        return PendingIntent.getBroadcast(
            appContext,
            alarmId.hashCode(),
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
    }

    private fun storageKey(alarmId: String): String = "$ALARM_KEY_PREFIX$alarmId"

    companion object {
        private const val STORAGE_NAME = "lumirise_system_alarms"
        private const val ALARM_KEY_PREFIX = "alarm_"
        private const val STALE_TOLERANCE_MS = 60_000L
    }
}
