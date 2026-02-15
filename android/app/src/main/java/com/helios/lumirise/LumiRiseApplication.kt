package com.helios.lumirise

import android.app.Application
import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import android.os.Build
import androidx.work.Constraints
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.NetworkType
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import com.helios.lumirise.workers.AlarmSyncWorker
import java.util.concurrent.TimeUnit

class LumiRiseApplication : Application() {
    lateinit var appContainer: AppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        appContainer = AppContainer(this)
        createNotificationChannel()
        schedulePeriodicSync()
    }

    private fun schedulePeriodicSync() {
        val constraints = Constraints.Builder()
            .setRequiredNetworkType(NetworkType.CONNECTED)
            .build()

        val request = PeriodicWorkRequestBuilder<AlarmSyncWorker>(15, TimeUnit.MINUTES)
            .setConstraints(constraints)
            .build()

        WorkManager.getInstance(this).enqueueUniquePeriodicWork(
            AlarmSyncWorker.PERIODIC_WORK_NAME,
            ExistingPeriodicWorkPolicy.UPDATE,
            request
        )
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return
        }

        val syncChannel = NotificationChannel(
            AlarmSyncWorker.NOTIFICATION_CHANNEL_ID,
            "Alarm Synchronization",
            NotificationManager.IMPORTANCE_HIGH
        ).apply {
            description = "Warns about mismatch between Android and LumiRise alarms."
        }

        val triggerChannel = NotificationChannel(
            com.helios.lumirise.workers.SystemAlarmReceiver.NOTIFICATION_CHANNEL_ID,
            "Alarm Trigger Events",
            NotificationManager.IMPORTANCE_HIGH
        ).apply {
            description = "Shows local alarm trigger notifications."
        }

        val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        manager.createNotificationChannel(syncChannel)
        manager.createNotificationChannel(triggerChannel)
    }
}
