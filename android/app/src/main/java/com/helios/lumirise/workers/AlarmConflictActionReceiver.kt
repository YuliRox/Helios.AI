package com.helios.lumirise.workers

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
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

        WorkManager.getInstance(context).enqueue(workRequest)
    }

    companion object {
        const val ACTION_SYNC_REMOTE_TO_SYSTEM =
            "com.helios.lumirise.action.SYNC_REMOTE_TO_SYSTEM"
        const val ACTION_SYNC_SYSTEM_TO_REMOTE =
            "com.helios.lumirise.action.SYNC_SYSTEM_TO_REMOTE"
    }
}
