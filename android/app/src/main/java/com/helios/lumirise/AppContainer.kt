package com.helios.lumirise

import android.content.Context
import androidx.room.Room
import com.helios.lumirise.api.SettingsBackedLightAlarmApiProvider
import com.helios.lumirise.data.local.LumiRiseDatabase
import com.helios.lumirise.data.prefs.SyncPreferencesStore
import com.helios.lumirise.data.repository.AlarmRepository
import com.helios.lumirise.domain.sync.ApiReachabilityHomeNetworkChecker
import com.helios.lumirise.domain.sync.AlarmSyncManager
import com.helios.lumirise.domain.sync.AndroidSystemAlarmGateway

class AppContainer(context: Context) {
    private val appContext = context.applicationContext

    private val database: LumiRiseDatabase = Room.databaseBuilder(
        appContext,
        LumiRiseDatabase::class.java,
        "lumi_rise_android.db"
    ).build()

    private val alarmDao = database.alarmDao()
    val syncPreferencesStore = SyncPreferencesStore(appContext)
    private val lightAlarmApiProvider = SettingsBackedLightAlarmApiProvider(syncPreferencesStore)

    private val systemAlarmGateway = AndroidSystemAlarmGateway(appContext)
    private val homeNetworkChecker = ApiReachabilityHomeNetworkChecker(
        context = appContext,
        lightAlarmApiProvider = lightAlarmApiProvider
    )
    private val alarmSyncManager = AlarmSyncManager(
        alarmDao = alarmDao,
        lightAlarmApiProvider = lightAlarmApiProvider,
        systemAlarmGateway = systemAlarmGateway
    )

    val alarmRepository = AlarmRepository(
        alarmDao = alarmDao,
        syncManager = alarmSyncManager,
        homeNetworkChecker = homeNetworkChecker,
        systemAlarmGateway = systemAlarmGateway,
        preferencesStore = syncPreferencesStore
    )
}
