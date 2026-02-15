package com.helios.lumirise.domain.sync

import com.helios.lumirise.data.local.AlarmEntity

interface SystemAlarmGateway {
    fun getNextAlarmEpochMillis(): Long?
    fun scheduleAlarm(alarm: AlarmEntity)
    fun cancelAlarm(alarmId: String)
}
