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

    override fun getNextAlarmEpochMillis(): Long? = alarmManager.nextAlarmClock?.triggerTime

    override fun scheduleAlarm(alarm: AlarmEntity) {
        if (!alarm.enabled) {
            cancelAlarm(alarm.id)
            return
        }

        val showIntent = createPendingIntent(alarm.id, alarm.label)
        val alarmClockInfo = AlarmManager.AlarmClockInfo(alarm.timestamp, showIntent)
        alarmManager.setAlarmClock(alarmClockInfo, showIntent)
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
}
