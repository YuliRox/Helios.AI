package com.helios.lumirise.workers

import android.Manifest
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.helios.lumirise.LumiRiseApplication
import com.helios.lumirise.MainActivity
import com.helios.lumirise.R
import com.helios.lumirise.domain.sync.ConflictResolutionStrategy
import kotlinx.coroutines.flow.first

class AlarmSyncWorker(
    appContext: Context,
    params: WorkerParameters
) : CoroutineWorker(appContext, params) {

    override suspend fun doWork(): Result {
        val app = applicationContext as LumiRiseApplication
        val repository = app.appContainer.alarmRepository

        val actionStrategy = inputData.getString(KEY_STRATEGY)?.toStrategy() ?: ConflictResolutionStrategy.NONE
        val homePresence = repository.refreshHomePresence()

        if (!homePresence.isAtHomeNetwork) {
            return Result.success()
        }

        if (homePresence.returnedHomeWithChangedSystemAlarm && actionStrategy == ConflictResolutionStrategy.NONE) {
            showReturnedHomeDecisionNotification(applicationContext)
            return Result.success()
        }

        val autoSyncEnabled = app.appContainer.syncPreferencesStore.autoSyncEnabled.first()

        val effectiveStrategy = when {
            actionStrategy != ConflictResolutionStrategy.NONE -> actionStrategy
            autoSyncEnabled -> ConflictResolutionStrategy.REMOTE_TO_SYSTEM
            else -> ConflictResolutionStrategy.NONE
        }

        val syncOutcome = repository.syncNow(effectiveStrategy)
        if (!syncOutcome.success) {
            return Result.retry()
        }

        if (syncOutcome.hasDiscrepancy && !autoSyncEnabled && actionStrategy == ConflictResolutionStrategy.NONE) {
            showDiscrepancyNotification(applicationContext)
        }

        return Result.success()
    }

    private fun showDiscrepancyNotification(context: Context) {
        if (!hasNotificationPermission(context)) {
            return
        }

        val syncRemoteIntent = Intent(context, AlarmConflictActionReceiver::class.java).apply {
            action = AlarmConflictActionReceiver.ACTION_SYNC_REMOTE_TO_SYSTEM
        }

        val syncSystemIntent = Intent(context, AlarmConflictActionReceiver::class.java).apply {
            action = AlarmConflictActionReceiver.ACTION_SYNC_SYSTEM_TO_REMOTE
        }

        val syncRemotePendingIntent = PendingIntent.getBroadcast(
            context,
            100,
            syncRemoteIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val syncSystemPendingIntent = PendingIntent.getBroadcast(
            context,
            101,
            syncSystemIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val notification = NotificationCompat.Builder(context, NOTIFICATION_CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_dialog_alert)
            .setContentTitle("LumiRise Sync")
            .setContentText(context.getString(R.string.sync_warning))
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setAutoCancel(true)
            .addAction(0, "Lichtwecker -> Android", syncRemotePendingIntent)
            .addAction(0, "Android -> Lichtwecker", syncSystemPendingIntent)
            .build()

        val manager = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        manager.notify(DISCREPANCY_NOTIFICATION_ID, notification)
    }

    private fun showReturnedHomeDecisionNotification(context: Context) {
        if (!hasNotificationPermission(context)) {
            return
        }

        val syncNowIntent = Intent(context, AlarmConflictActionReceiver::class.java).apply {
            action = AlarmConflictActionReceiver.ACTION_SYNC_SYSTEM_TO_REMOTE
        }

        val reviewIntent = Intent(context, MainActivity::class.java)

        val syncNowPendingIntent = PendingIntent.getBroadcast(
            context,
            102,
            syncNowIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val reviewPendingIntent = PendingIntent.getActivity(
            context,
            103,
            reviewIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val notification = NotificationCompat.Builder(context, NOTIFICATION_CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_dialog_info)
            .setContentTitle("LumiRise Sync")
            .setContentText("System-Wecker wurden außerhalb des Heimnetzes geändert. Jetzt synchronisieren?")
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setAutoCancel(true)
            .setContentIntent(reviewPendingIntent)
            .addAction(0, "Jetzt synchronisieren", syncNowPendingIntent)
            .addAction(0, "Zuerst anpassen", reviewPendingIntent)
            .build()

        val manager = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        manager.notify(RETURN_HOME_NOTIFICATION_ID, notification)
    }

    private fun hasNotificationPermission(context: Context): Boolean =
        ContextCompat.checkSelfPermission(
            context,
            Manifest.permission.POST_NOTIFICATIONS
        ) == PackageManager.PERMISSION_GRANTED

    private fun String.toStrategy(): ConflictResolutionStrategy = when (this) {
        STRATEGY_REMOTE_TO_SYSTEM -> ConflictResolutionStrategy.REMOTE_TO_SYSTEM
        STRATEGY_SYSTEM_TO_REMOTE -> ConflictResolutionStrategy.SYSTEM_TO_REMOTE
        else -> ConflictResolutionStrategy.NONE
    }

    companion object {
        const val PERIODIC_WORK_NAME = "alarm-sync-periodic"
        const val NOTIFICATION_CHANNEL_ID = "alarm_sync_discrepancy"

        const val KEY_STRATEGY = "strategy"
        const val STRATEGY_REMOTE_TO_SYSTEM = "remote_to_system"
        const val STRATEGY_SYSTEM_TO_REMOTE = "system_to_remote"

        const val DISCREPANCY_NOTIFICATION_ID = 4001
        const val RETURN_HOME_NOTIFICATION_ID = 4003
    }
}
