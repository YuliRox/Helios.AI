package com.helios.lumirise

import android.Manifest
import android.content.pm.PackageManager
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import androidx.lifecycle.viewmodel.compose.viewModel
import com.helios.lumirise.ui.AlarmScreen
import com.helios.lumirise.ui.AlarmViewModel

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        requestLocationPermissionIfNeeded()

        val repository = (application as LumiRiseApplication).appContainer.alarmRepository

        setContent {
            val alarmViewModel: AlarmViewModel = viewModel(
                factory = AlarmViewModel.Factory(repository)
            )

            Surface(modifier = Modifier.fillMaxSize()) {
                AlarmScreen(alarmViewModel)
            }
        }
    }

    private fun requestLocationPermissionIfNeeded() {
        val permissionsToRequest = mutableListOf<String>()

        if (
            ContextCompat.checkSelfPermission(
                this,
                Manifest.permission.ACCESS_FINE_LOCATION
            ) != PackageManager.PERMISSION_GRANTED
        ) {
            permissionsToRequest += Manifest.permission.ACCESS_FINE_LOCATION
        }

        if (
            android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.TIRAMISU &&
            ContextCompat.checkSelfPermission(
                this,
                Manifest.permission.POST_NOTIFICATIONS
            ) != PackageManager.PERMISSION_GRANTED
        ) {
            permissionsToRequest += Manifest.permission.POST_NOTIFICATIONS
        }

        if (permissionsToRequest.isNotEmpty()) {
            ActivityCompat.requestPermissions(
                this,
                permissionsToRequest.toTypedArray(),
                REQUEST_LOCATION_PERMISSION
            )
        }
    }

    companion object {
        private const val REQUEST_LOCATION_PERMISSION = 1001
    }
}
