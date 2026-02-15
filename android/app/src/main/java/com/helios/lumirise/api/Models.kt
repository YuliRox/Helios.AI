package com.helios.lumirise.api

data class AlarmResponseDto(
    val id: String,
    val name: String?,
    val enabled: Boolean,
    val daysOfWeek: List<String>?,
    val time: String?,
    val rampMode: String?,
    val createdAtUtc: String?,
    val updatedAtUtc: String?
)

data class AlarmUpsertRequestDto(
    val name: String,
    val daysOfWeek: List<String>,
    val time: String,
    val enabled: Boolean,
    val rampMode: String
)
