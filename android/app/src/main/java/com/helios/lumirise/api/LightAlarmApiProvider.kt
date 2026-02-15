package com.helios.lumirise.api

interface LightAlarmApiProvider {
    suspend fun getApi(): LightAlarmApi
}
