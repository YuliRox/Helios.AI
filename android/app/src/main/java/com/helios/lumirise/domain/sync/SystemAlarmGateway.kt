package com.helios.lumirise.domain.sync

import com.helios.lumirise.data.local.AlarmEntity

interface SystemAlarmGateway {
    fun getScheduledAlarmEpochMillis(): Set<Long>
    fun getNextAlarmEpochMillis(): Long?
    fun scheduleAlarm(alarm: AlarmEntity)
    fun cancelAlarm(alarmId: String)
}
