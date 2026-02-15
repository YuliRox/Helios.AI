package com.helios.lumirise

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
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
}
