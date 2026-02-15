package com.helios.lumirise.workers

import android.app.NotificationManager
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.work.ExistingWorkPolicy
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.workDataOf

class AlarmConflictActionReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val strategy = when (intent.action) {
            ACTION_SYNC_REMOTE_TO_SYSTEM -> AlarmSyncWorker.STRATEGY_REMOTE_TO_SYSTEM
            ACTION_SYNC_SYSTEM_TO_REMOTE -> AlarmSyncWorker.STRATEGY_SYSTEM_TO_REMOTE
            else -> return
        }

        val workRequest = OneTimeWorkRequestBuilder<AlarmSyncWorker>()
            .setInputData(workDataOf(AlarmSyncWorker.KEY_STRATEGY to strategy))
            .build()

        WorkManager.getInstance(context).enqueueUniqueWork(
            UNIQUE_WORK_NAME,
            ExistingWorkPolicy.REPLACE,
            workRequest
        )

        val notificationManager =
            context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        notificationManager.cancel(AlarmSyncWorker.DISCREPANCY_NOTIFICATION_ID)
        notificationManager.cancel(AlarmSyncWorker.RETURN_HOME_NOTIFICATION_ID)
    }

    companion object {
        const val ACTION_SYNC_REMOTE_TO_SYSTEM =
            "com.helios.lumirise.action.SYNC_REMOTE_TO_SYSTEM"
        const val ACTION_SYNC_SYSTEM_TO_REMOTE =
            "com.helios.lumirise.action.SYNC_SYSTEM_TO_REMOTE"
        private const val UNIQUE_WORK_NAME = "alarm-conflict-resolution"
    }
}
