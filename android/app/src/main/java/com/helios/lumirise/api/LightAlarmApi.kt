package com.helios.lumirise.api

import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path

// Contract mirrored from samples/swagger.json.
interface LightAlarmApi {
    @GET("api/Alarms")
    suspend fun getAlarms(): List<AlarmResponseDto>

    @POST("api/Alarms")
    suspend fun createAlarm(@Body request: AlarmUpsertRequestDto): AlarmResponseDto

    @PUT("api/Alarms/{id}")
    suspend fun updateAlarm(
        @Path("id") id: String,
        @Body request: AlarmUpsertRequestDto
    ): AlarmResponseDto

    @DELETE("api/Alarms/{id}")
    suspend fun deleteAlarm(@Path("id") id: String)
}
