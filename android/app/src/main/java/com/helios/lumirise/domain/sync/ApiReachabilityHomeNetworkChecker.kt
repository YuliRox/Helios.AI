package com.helios.lumirise.domain.sync

import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.net.wifi.WifiInfo
import android.net.wifi.WifiManager
import com.helios.lumirise.api.LightAlarmApiProvider
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeoutOrNull

class ApiReachabilityHomeNetworkChecker(
    context: Context,
    private val lightAlarmApiProvider: LightAlarmApiProvider,
    private val ioDispatcher: CoroutineDispatcher = Dispatchers.IO
) : HomeNetworkChecker {
    private val appContext = context.applicationContext
    private val connectivityManager =
        appContext.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
    private val wifiManager =
        appContext.getSystemService(Context.WIFI_SERVICE) as WifiManager

    override suspend fun getCurrentWifiSsid(): String? = withContext(ioDispatcher) {
        val activeNetwork = connectivityManager.activeNetwork ?: return@withContext null
        val capabilities = connectivityManager.getNetworkCapabilities(activeNetwork)
            ?: return@withContext null

        if (!capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)) {
            return@withContext null
        }

        val transportSsid = (capabilities.transportInfo as? WifiInfo)?.ssid
        val rawSsid = transportSsid ?: legacyWifiSsid() ?: return@withContext null
        normalizeSsid(rawSsid)
    }

    override suspend fun isApiReachable(): Boolean = withContext(ioDispatcher) {
        val reachable = withTimeoutOrNull(HOME_CHECK_TIMEOUT_MS) {
            runCatching {
                val api = lightAlarmApiProvider.getApi()
                api.getAlarms()
            }.isSuccess
        }

        reachable == true
    }

    private fun normalizeSsid(rawSsid: String): String? {
        val normalized = rawSsid
            .removePrefix("\"")
            .removeSuffix("\"")
            .trim()

        if (normalized.isBlank() || normalized == "<unknown ssid>") {
            return null
        }

        return normalized
    }

    @Suppress("DEPRECATION")
    private fun legacyWifiSsid(): String? = wifiManager.connectionInfo?.ssid

    companion object {
        private const val HOME_CHECK_TIMEOUT_MS = 3_000L
    }
}
