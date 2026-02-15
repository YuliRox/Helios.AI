package com.helios.lumirise.api

import com.helios.lumirise.data.prefs.SyncPreferencesStore

class SettingsBackedLightAlarmApiProvider(
    private val preferencesStore: SyncPreferencesStore
) : LightAlarmApiProvider {
    @Volatile
    private var cachedBaseUrl: String? = null

    @Volatile
    private var cachedApi: LightAlarmApi? = null

    override suspend fun getApi(): LightAlarmApi {
        val baseUrl = preferencesStore.getApiBaseUrl()
        val existingApi = cachedApi
        if (existingApi != null && cachedBaseUrl == baseUrl) {
            return existingApi
        }

        return synchronized(this) {
            if (cachedApi != null && cachedBaseUrl == baseUrl) {
                cachedApi!!
            } else {
                val newApi = ApiFactory.create(baseUrl)
                cachedBaseUrl = baseUrl
                cachedApi = newApi
                newApi
            }
        }
    }
}
