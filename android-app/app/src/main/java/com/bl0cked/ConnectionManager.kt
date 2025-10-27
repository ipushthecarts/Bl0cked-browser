package com.bl0cked

import android.content.Context
import android.content.SharedPreferences
import android.util.Log
import com.bl0cked.models.Chore
import com.bl0cked.models.ChoreData
import com.bl0cked.models.LockStatus
import com.bl0cked.models.PairingInfo
import com.google.gson.Gson
import kotlinx.coroutines.*
import okhttp3.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.IOException

/**
 * Manages connection to the PC via local HTTP or Firebase (internet mode).
 * Automatically switches between modes based on availability.
 */
class ConnectionManager(private val context: Context) {

    companion object {
        private const val TAG = "ConnectionManager"
        private const val PREFS_NAME = "Bl0ckedPrefs"
        private const val PREF_PAIRED = "paired"
        private const val PREF_PC_IDENTIFIER = "pc_identifier"
        private const val PREF_SHARED_SECRET = "shared_secret"
        private const val PREF_PC_IP = "pc_ip"
        private const val LOCAL_PORT = 45823
        private const val CONNECTION_TIMEOUT_MS = 3000L
    }

    private val prefs: SharedPreferences = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
    private val gson = Gson()
    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(CONNECTION_TIMEOUT_MS, java.util.concurrent.TimeUnit.MILLISECONDS)
        .readTimeout(CONNECTION_TIMEOUT_MS, java.util.concurrent.TimeUnit.MILLISECONDS)
        .build()

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    var firebaseService: FirebaseService? = null
    var onStatusUpdate: ((LockStatus) -> Unit)? = null
    var onChoresUpdate: ((List<Chore>) -> Unit)? = null

    // Connection mode
    private var useLocalMode = true

    /**
     * Checks if the app is paired with a PC
     */
    fun isPaired(): Boolean {
        return prefs.getBoolean(PREF_PAIRED, false)
    }

    /**
     * Gets pairing information
     */
    fun getPairingInfo(): PairingInfo {
        return PairingInfo(
            paired = prefs.getBoolean(PREF_PAIRED, false),
            pcIdentifier = prefs.getString(PREF_PC_IDENTIFIER, null),
            sharedSecret = prefs.getString(PREF_SHARED_SECRET, null)
        )
    }

    /**
     * Attempts to pair with a PC using a 6-digit code
     */
    suspend fun pairWithPc(pairingCode: String, pcIpAddress: String): Result<Unit> = withContext(Dispatchers.IO) {
        try {
            val deviceId = android.provider.Settings.Secure.getString(
                context.contentResolver,
                android.provider.Settings.Secure.ANDROID_ID
            )

            val requestBody = mapOf(
                "code" to pairingCode,
                "deviceId" to deviceId,
                "fcmToken" to "" // TODO: Get actual FCM token
            )

            val json = gson.toJson(requestBody)
            val body = json.toRequestBody("application/json".toMediaType())

            val request = Request.Builder()
                .url("http://$pcIpAddress:$LOCAL_PORT/pair")
                .post(body)
                .build()

            val response = httpClient.newCall(request).execute()
            val responseBody = response.body?.string()

            if (response.isSuccessful && responseBody != null) {
                val pairingResponse = gson.fromJson(responseBody, PairingResponse::class.java)

                if (pairingResponse.success) {
                    // Save pairing info
                    prefs.edit().apply {
                        putBoolean(PREF_PAIRED, true)
                        putString(PREF_PC_IDENTIFIER, pairingResponse.pcIdentifier)
                        putString(PREF_SHARED_SECRET, pairingResponse.sharedSecret)
                        putString(PREF_PC_IP, pcIpAddress)
                        apply()
                    }

                    Log.d(TAG, "Pairing successful")
                    return@withContext Result.success(Unit)
                } else {
                    return@withContext Result.failure(Exception(pairingResponse.error ?: "Pairing failed"))
                }
            } else {
                return@withContext Result.failure(Exception("HTTP ${response.code}: ${responseBody}"))
            }
        } catch (e: Exception) {
            Log.e(TAG, "Pairing error", e)
            return@withContext Result.failure(e)
        }
    }

    /**
     * Gets the current lock status from the PC
     */
    suspend fun getStatus(): Result<LockStatus> = withContext(Dispatchers.IO) {
        if (!isPaired()) {
            return@withContext Result.failure(Exception("Not paired"))
        }

        // Try local mode first
        val localResult = getStatusLocal()
        if (localResult.isSuccess) {
            useLocalMode = true
            return@withContext localResult
        }

        // Fall back to Firebase
        Log.d(TAG, "Local mode failed, trying Firebase")
        useLocalMode = false
        return@withContext getStatusFirebase()
    }

    /**
     * Gets status via local HTTP
     */
    private suspend fun getStatusLocal(): Result<LockStatus> = withContext(Dispatchers.IO) {
        try {
            val pcIp = prefs.getString(PREF_PC_IP, null) ?: return@withContext Result.failure(Exception("No PC IP saved"))
            val sharedSecret = prefs.getString(PREF_SHARED_SECRET, null) ?: return@withContext Result.failure(Exception("No shared secret"))

            val request = Request.Builder()
                .url("http://$pcIp:$LOCAL_PORT/status")
                .get()
                .addHeader("X-Auth-Token", sharedSecret)
                .build()

            val response = httpClient.newCall(request).execute()
            val responseBody = response.body?.string()

            if (response.isSuccessful && responseBody != null) {
                val status = gson.fromJson(responseBody, LockStatus::class.java)
                return@withContext Result.success(status)
            } else {
                return@withContext Result.failure(Exception("HTTP ${response.code}"))
            }
        } catch (e: Exception) {
            Log.e(TAG, "Local status error", e)
            return@withContext Result.failure(e)
        }
    }

    /**
     * Gets status via Firebase
     */
    private suspend fun getStatusFirebase(): Result<LockStatus> {
        return firebaseService?.getStatus() ?: Result.failure(Exception("Firebase not initialized"))
    }

    /**
     * Sends lock command to PC
     */
    suspend fun sendLockCommand(): Result<Unit> = withContext(Dispatchers.IO) {
        if (!isPaired()) {
            return@withContext Result.failure(Exception("Not paired"))
        }

        if (useLocalMode) {
            val result = sendLockCommandLocal()
            if (result.isSuccess) return@withContext result

            // Fall back to Firebase
            useLocalMode = false
        }

        return@withContext sendLockCommandFirebase()
    }

    /**
     * Sends lock command via local HTTP
     */
    private suspend fun sendLockCommandLocal(): Result<Unit> = withContext(Dispatchers.IO) {
        try {
            val pcIp = prefs.getString(PREF_PC_IP, null) ?: return@withContext Result.failure(Exception("No PC IP"))
            val sharedSecret = prefs.getString(PREF_SHARED_SECRET, null) ?: return@withContext Result.failure(Exception("No shared secret"))

            val requestBody = mapOf(
                "action" to "lock",
                "token" to sharedSecret
            )

            val json = gson.toJson(requestBody)
            val body = json.toRequestBody("application/json".toMediaType())

            val request = Request.Builder()
                .url("http://$pcIp:$LOCAL_PORT/lock")
                .post(body)
                .build()

            val response = httpClient.newCall(request).execute()

            return@withContext if (response.isSuccessful) {
                Result.success(Unit)
            } else {
                Result.failure(Exception("HTTP ${response.code}"))
            }
        } catch (e: Exception) {
            Log.e(TAG, "Local lock command error", e)
            return@withContext Result.failure(e)
        }
    }

    /**
     * Sends lock command via Firebase
     */
    private suspend fun sendLockCommandFirebase(): Result<Unit> {
        return firebaseService?.sendLockCommand() ?: Result.failure(Exception("Firebase not initialized"))
    }

    /**
     * Sends unlock command to PC
     */
    suspend fun sendUnlockCommand(): Result<Unit> = withContext(Dispatchers.IO) {
        if (!isPaired()) {
            return@withContext Result.failure(Exception("Not paired"))
        }

        if (useLocalMode) {
            val result = sendUnlockCommandLocal()
            if (result.isSuccess) return@withContext result

            // Fall back to Firebase
            useLocalMode = false
        }

        return@withContext sendUnlockCommandFirebase()
    }

    /**
     * Sends unlock command via local HTTP
     */
    private suspend fun sendUnlockCommandLocal(): Result<Unit> = withContext(Dispatchers.IO) {
        try {
            val pcIp = prefs.getString(PREF_PC_IP, null) ?: return@withContext Result.failure(Exception("No PC IP"))
            val sharedSecret = prefs.getString(PREF_SHARED_SECRET, null) ?: return@withContext Result.failure(Exception("No shared secret"))

            val requestBody = mapOf(
                "action" to "unlock",
                "token" to sharedSecret
            )

            val json = gson.toJson(requestBody)
            val body = json.toRequestBody("application/json".toMediaType())

            val request = Request.Builder()
                .url("http://$pcIp:$LOCAL_PORT/unlock")
                .post(body)
                .build()

            val response = httpClient.newCall(request).execute()

            return@withContext if (response.isSuccessful) {
                Result.success(Unit)
            } else {
                Result.failure(Exception("HTTP ${response.code}"))
            }
        } catch (e: Exception) {
            Log.e(TAG, "Local unlock command error", e)
            return@withContext Result.failure(e)
        }
    }

    /**
     * Sends unlock command via Firebase
     */
    private suspend fun sendUnlockCommandFirebase(): Result<Unit> {
        return firebaseService?.sendUnlockCommand() ?: Result.failure(Exception("Firebase not initialized"))
    }

    /**
     * Gets the chore list from PC
     */
    suspend fun getChores(): Result<List<Chore>> = withContext(Dispatchers.IO) {
        if (!isPaired()) {
            return@withContext Result.failure(Exception("Not paired"))
        }

        if (useLocalMode) {
            val result = getChoresLocal()
            if (result.isSuccess) return@withContext result

            useLocalMode = false
        }

        return@withContext getChoresFirebase()
    }

    /**
     * Gets chores via local HTTP
     */
    private suspend fun getChoresLocal(): Result<List<Chore>> = withContext(Dispatchers.IO) {
        try {
            val pcIp = prefs.getString(PREF_PC_IP, null) ?: return@withContext Result.failure(Exception("No PC IP"))
            val sharedSecret = prefs.getString(PREF_SHARED_SECRET, null) ?: return@withContext Result.failure(Exception("No shared secret"))

            val request = Request.Builder()
                .url("http://$pcIp:$LOCAL_PORT/chores")
                .get()
                .addHeader("X-Auth-Token", sharedSecret)
                .build()

            val response = httpClient.newCall(request).execute()
            val responseBody = response.body?.string()

            if (response.isSuccessful && responseBody != null) {
                val choreData = gson.fromJson(responseBody, ChoreData::class.java)
                return@withContext Result.success(choreData.chores)
            } else {
                return@withContext Result.failure(Exception("HTTP ${response.code}"))
            }
        } catch (e: Exception) {
            Log.e(TAG, "Local get chores error", e)
            return@withContext Result.failure(e)
        }
    }

    /**
     * Gets chores via Firebase
     */
    private suspend fun getChoresFirebase(): Result<List<Chore>> {
        return firebaseService?.getChores() ?: Result.failure(Exception("Firebase not initialized"))
    }

    /**
     * Updates the chore list on PC
     */
    suspend fun updateChores(chores: List<Chore>): Result<Unit> = withContext(Dispatchers.IO) {
        if (!isPaired()) {
            return@withContext Result.failure(Exception("Not paired"))
        }

        if (useLocalMode) {
            val result = updateChoresLocal(chores)
            if (result.isSuccess) return@withContext result

            useLocalMode = false
        }

        return@withContext updateChoresFirebase(chores)
    }

    /**
     * Updates chores via local HTTP
     */
    private suspend fun updateChoresLocal(chores: List<Chore>): Result<Unit> = withContext(Dispatchers.IO) {
        try {
            val pcIp = prefs.getString(PREF_PC_IP, null) ?: return@withContext Result.failure(Exception("No PC IP"))
            val sharedSecret = prefs.getString(PREF_SHARED_SECRET, null) ?: return@withContext Result.failure(Exception("No shared secret"))

            val requestBody = mapOf(
                "chores" to chores,
                "token" to sharedSecret
            )

            val json = gson.toJson(requestBody)
            val body = json.toRequestBody("application/json".toMediaType())

            val request = Request.Builder()
                .url("http://$pcIp:$LOCAL_PORT/chores/update")
                .post(body)
                .build()

            val response = httpClient.newCall(request).execute()

            return@withContext if (response.isSuccessful) {
                Result.success(Unit)
            } else {
                Result.failure(Exception("HTTP ${response.code}"))
            }
        } catch (e: Exception) {
            Log.e(TAG, "Local update chores error", e)
            return@withContext Result.failure(e)
        }
    }

    /**
     * Updates chores via Firebase
     */
    private suspend fun updateChoresFirebase(chores: List<Chore>): Result<Unit> {
        return firebaseService?.updateChores(chores) ?: Result.failure(Exception("Firebase not initialized"))
    }

    /**
     * Cleanup
     */
    fun cleanup() {
        scope.cancel()
    }
}

// Response DTOs
private data class PairingResponse(
    val success: Boolean,
    val sharedSecret: String?,
    val pcIdentifier: String?,
    val error: String?
)
