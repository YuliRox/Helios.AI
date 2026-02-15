package com.helios.lumirise.data.prefs

import android.content.Context
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.longPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map

private const val STORE_NAME = "lumi_rise_sync_preferences"
private val Context.dataStore by preferencesDataStore(name = STORE_NAME)

class SyncPreferencesStore(private val context: Context) {
    private val autoSyncKey = booleanPreferencesKey("auto_sync_enabled")
    private val lastKnownHomeStateKey = booleanPreferencesKey("last_known_home_state")
    private val awayBaselineSystemAlarmEpochKey = longPreferencesKey("away_baseline_system_alarm_epoch")
    private val apiBaseUrlKey = stringPreferencesKey("api_base_url")
    private val homeNetworkSsidKey = stringPreferencesKey("home_network_ssid")

    val autoSyncEnabled: Flow<Boolean> = context.dataStore.data.map { prefs ->
        prefs[autoSyncKey] ?: true
    }

    val apiBaseUrl: Flow<String> = context.dataStore.data.map { prefs ->
        normalizeBaseUrl(prefs[apiBaseUrlKey] ?: DEFAULT_API_BASE_URL)
    }

    val homeNetworkSsid: Flow<String?> = context.dataStore.data.map { prefs ->
        normalizeSsid(prefs[homeNetworkSsidKey])
    }

    suspend fun setAutoSyncEnabled(enabled: Boolean) {
        context.dataStore.edit { prefs ->
            prefs[autoSyncKey] = enabled
        }
    }

    suspend fun getLastKnownHomeState(defaultValue: Boolean = true): Boolean {
        val prefs = context.dataStore.data.first()
        return prefs[lastKnownHomeStateKey] ?: defaultValue
    }

    suspend fun setLastKnownHomeState(value: Boolean) {
        context.dataStore.edit { prefs ->
            prefs[lastKnownHomeStateKey] = value
        }
    }

    suspend fun getAwayBaselineSystemAlarmEpoch(): Long? {
        val prefs = context.dataStore.data.first()
        return prefs[awayBaselineSystemAlarmEpochKey]
    }

    suspend fun setAwayBaselineSystemAlarmEpoch(value: Long?) {
        context.dataStore.edit { prefs ->
            if (value == null) {
                prefs.remove(awayBaselineSystemAlarmEpochKey)
            } else {
                prefs[awayBaselineSystemAlarmEpochKey] = value
            }
        }
    }

    suspend fun getApiBaseUrl(): String {
        val prefs = context.dataStore.data.first()
        return normalizeBaseUrl(prefs[apiBaseUrlKey] ?: DEFAULT_API_BASE_URL)
    }

    suspend fun setApiBaseUrl(url: String) {
        context.dataStore.edit { prefs ->
            prefs[apiBaseUrlKey] = normalizeBaseUrl(url)
        }
    }

    suspend fun getHomeNetworkSsid(): String? {
        val prefs = context.dataStore.data.first()
        return normalizeSsid(prefs[homeNetworkSsidKey])
    }

    suspend fun setHomeNetworkSsid(ssid: String?) {
        context.dataStore.edit { prefs ->
            val normalized = normalizeSsid(ssid)
            if (normalized == null) {
                prefs.remove(homeNetworkSsidKey)
            } else {
                prefs[homeNetworkSsidKey] = normalized
            }
        }
    }

    private fun normalizeBaseUrl(value: String): String {
        val trimmed = value.trim()
        val nonEmpty = if (trimmed.isEmpty()) DEFAULT_API_BASE_URL else trimmed
        return if (nonEmpty.endsWith('/')) nonEmpty else "$nonEmpty/"
    }

    private fun normalizeSsid(value: String?): String? {
        val normalized = value?.trim()?.removePrefix("\"")?.removeSuffix("\"")
        return if (normalized.isNullOrEmpty()) null else normalized
    }

    companion object {
        const val DEFAULT_API_BASE_URL = "http://10.0.2.2:8080/"
    }
}
