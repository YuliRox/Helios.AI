package com.helios.lumirise.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.helios.lumirise.domain.model.Alarm
import com.helios.lumirise.domain.model.SyncStatus
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AlarmScreen(viewModel: AlarmViewModel) {
    val state by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    val lifecycleOwner = LocalLifecycleOwner.current
    var showCreateDialog by remember { mutableStateOf(false) }
    var showSettingsDialog by remember { mutableStateOf(false) }

    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) {
                viewModel.refreshHomePresence()
                viewModel.refreshCurrentNetworkSsid()
            }
        }

        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    LaunchedEffect(state.errorEventId) {
        val message = state.errorMessage ?: return@LaunchedEffect
        snackbarHostState.showSnackbar(message)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("LumiRise Alarm Sync") },
                actions = {
                    TextButton(onClick = { showSettingsDialog = true }) {
                        Text("Settings")
                    }
                }
            )
        },
        snackbarHost = { SnackbarHost(hostState = snackbarHostState) },
        floatingActionButton = {
            FloatingActionButton(onClick = {
                if (state.isAtHomeNetwork) {
                    showCreateDialog = true
                }
            }) {
                Text("+")
            }
        }
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(horizontal = 16.dp, vertical = 12.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("Auto-Sync", fontWeight = FontWeight.SemiBold)
                Switch(
                    checked = state.autoSyncEnabled,
                    onCheckedChange = viewModel::onAutoSyncChanged,
                    enabled = state.isAtHomeNetwork
                )
            }

            if (!state.isAtHomeNetwork) {
                HomeNetworkRequiredBanner()
            }

            if (state.showDiscrepancyWarning) {
                WarningBanner(
                    onSyncNow = { viewModel.syncNow() },
                    onRemoteToSystem = viewModel::applyRemoteToSystemResolution,
                    onSystemToRemote = viewModel::applySystemToRemoteResolution,
                    enabled = state.isAtHomeNetwork
                )
            }

            LazyColumn(
                modifier = Modifier.fillMaxSize(),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                items(state.alarms, key = { alarm -> alarm.id }) { alarm ->
                    AlarmListItem(alarm = alarm)
                }
            }
        }
    }

    if (showCreateDialog && state.isAtHomeNetwork) {
        NewAlarmDialog(
            onDismiss = { showCreateDialog = false },
            onSave = { label, time ->
                viewModel.createAlarm(label = label, timeInput = time)
                showCreateDialog = false
            }
        )
    }

    if (showSettingsDialog) {
        SettingsDialog(
            initialApiBaseUrl = state.apiBaseUrl,
            initialHomeSsid = state.homeNetworkSsid,
            currentNetworkSsid = state.currentNetworkSsid,
            onRefreshCurrentNetwork = viewModel::refreshCurrentNetworkSsid,
            onDismiss = { showSettingsDialog = false },
            onSave = { apiBaseUrl, homeSsid ->
                viewModel.saveNetworkSettings(apiBaseUrl, homeSsid)
                showSettingsDialog = false
            }
        )
    }
}

@Composable
private fun WarningBanner(
    onSyncNow: () -> Unit,
    onRemoteToSystem: () -> Unit,
    onSystemToRemote: () -> Unit,
    enabled: Boolean
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.errorContainer)
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Text(
                text = "Abweichung erkannt: Android-Wecker und Lichtwecker unterscheiden sich.",
                color = MaterialTheme.colorScheme.onErrorContainer,
                fontWeight = FontWeight.SemiBold
            )

            Spacer(modifier = Modifier.height(8.dp))

            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                TextButton(onClick = onSyncNow, enabled = enabled) { Text("Jetzt synchronisieren") }
                TextButton(onClick = onRemoteToSystem, enabled = enabled) { Text("Lichtwecker -> Android") }
                TextButton(onClick = onSystemToRemote, enabled = enabled) { Text("Android -> Lichtwecker") }
            }
        }
    }
}

@Composable
private fun HomeNetworkRequiredBanner() {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.secondaryContainer)
    ) {
        Text(
            text = "Nicht im Heimnetz: Lichtwecker-Aktionen sind deaktiviert.",
            modifier = Modifier.padding(12.dp),
            color = MaterialTheme.colorScheme.onSecondaryContainer,
            fontWeight = FontWeight.SemiBold
        )
    }
}

@Composable
private fun AlarmListItem(alarm: Alarm) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text(alarm.label, style = MaterialTheme.typography.titleMedium)
                Text(
                    text = Instant.ofEpochMilli(alarm.timestamp)
                        .atZone(ZoneId.systemDefault())
                        .format(DateTimeFormatter.ofPattern("EEE, HH:mm")),
                    style = MaterialTheme.typography.bodyMedium
                )
            }

            AssistChip(
                onClick = { },
                label = { Text(alarm.syncStatus.name) },
                leadingIcon = {
                    Text(
                        text = "●",
                        color = statusColor(alarm.syncStatus)
                    )
                }
            )
        }
    }
}

@Composable
private fun statusColor(status: SyncStatus): Color = when (status) {
    SyncStatus.SYNCED -> Color(0xFF2E7D32)
    SyncStatus.LOCAL_ONLY -> Color(0xFFF9A825)
    SyncStatus.REMOTE_ONLY -> Color(0xFF1565C0)
    SyncStatus.OUT_OF_SYNC -> Color(0xFFC62828)
    SyncStatus.ERROR -> Color(0xFF6A1B9A)
}

@Composable
private fun NewAlarmDialog(
    onDismiss: () -> Unit,
    onSave: (label: String, time: String) -> Unit
) {
    var label by remember { mutableStateOf("") }
    var time by remember { mutableStateOf("07:00") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Neuer Wecker") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = label,
                    onValueChange = { label = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Bezeichnung") }
                )
                OutlinedTextField(
                    value = time,
                    onValueChange = { time = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Uhrzeit (HH:mm)") }
                )
            }
        },
        confirmButton = {
            TextButton(onClick = { onSave(label, time) }) {
                Text("Speichern")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Abbrechen")
            }
        }
    )
}

@Composable
private fun SettingsDialog(
    initialApiBaseUrl: String,
    initialHomeSsid: String,
    currentNetworkSsid: String?,
    onRefreshCurrentNetwork: () -> Unit,
    onDismiss: () -> Unit,
    onSave: (apiBaseUrl: String, homeSsid: String) -> Unit
) {
    var apiBaseUrl by remember(initialApiBaseUrl) { mutableStateOf(initialApiBaseUrl) }
    var homeSsid by remember(initialHomeSsid) { mutableStateOf(initialHomeSsid) }

    LaunchedEffect(Unit) {
        onRefreshCurrentNetwork()
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Settings") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = apiBaseUrl,
                    onValueChange = { apiBaseUrl = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Lichtwecker API URI") }
                )

                OutlinedTextField(
                    value = homeSsid,
                    onValueChange = { homeSsid = it },
                    modifier = Modifier.fillMaxWidth(),
                    label = { Text("Home network SSID") }
                )

                Text(
                    text = "Aktuelles Netzwerk: ${currentNetworkSsid ?: "nicht erkannt"}",
                    style = MaterialTheme.typography.bodySmall
                )

                TextButton(
                    onClick = {
                        if (!currentNetworkSsid.isNullOrBlank()) {
                            homeSsid = currentNetworkSsid
                        }
                    },
                    enabled = !currentNetworkSsid.isNullOrBlank()
                ) {
                    Text("Use current network as home network")
                }
            }
        },
        confirmButton = {
            TextButton(onClick = { onSave(apiBaseUrl, homeSsid) }) {
                Text("Speichern")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Abbrechen")
            }
        }
    )
}
