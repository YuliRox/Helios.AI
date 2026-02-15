package com.helios.lumirise.domain.model

enum class SyncStatus {
    SYNCED,
    LOCAL_ONLY,
    REMOTE_ONLY,
    OUT_OF_SYNC,
    ERROR
}
