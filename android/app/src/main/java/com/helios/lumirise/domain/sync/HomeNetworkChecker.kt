package com.helios.lumirise.domain.sync

interface HomeNetworkChecker {
    suspend fun getCurrentWifiSsid(): String?
    suspend fun isApiReachable(): Boolean
}
