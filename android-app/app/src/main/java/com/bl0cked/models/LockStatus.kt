package com.bl0cked.models

/**
 * Represents the current lock status of the PC
 */
data class LockStatus(
    val locked: Boolean = false,
    val lockDuration: Long = 0,  // Duration in seconds
    val lockStartTime: Long = 0,  // Unix timestamp
    val paired: Boolean = false
)

/**
 * Represents pairing information
 */
data class PairingInfo(
    val paired: Boolean = false,
    val pcIdentifier: String? = null,
    val sharedSecret: String? = null
)
