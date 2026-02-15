package com.helios.lumirise.domain.sync

import com.helios.lumirise.data.local.AlarmEntity

interface SystemAlarmGateway {
    fun getScheduledAlarmEpochMillisByAlarmId(): Map<String, Long>
    fun getScheduledAlarmEpochMillis(): Set<Long>
    fun getNextAlarmEpochMillis(): Long?
    fun scheduleAlarm(alarm: AlarmEntity)
    fun cancelAlarm(alarmId: String)
}
