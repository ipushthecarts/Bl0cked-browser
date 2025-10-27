package com.bl0cked

import android.util.Log
import com.bl0cked.models.Chore
import com.bl0cked.models.LockStatus
import com.google.firebase.database.*
import com.google.firebase.database.ktx.database
import com.google.firebase.ktx.Firebase
import kotlinx.coroutines.tasks.await
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow

/**
 * Handles Firebase Realtime Database communication for remote control
 */
class FirebaseService(private val pcIdentifier: String) {

    companion object {
        private const val TAG = "FirebaseService"
        private const val BASE_PATH = "bl0cked/devices"
    }

    private val database: DatabaseReference = Firebase.database.reference
    private val devicePath = "$BASE_PATH/$pcIdentifier"

    /**
     * Gets current status from Firebase
     */
    suspend fun getStatus(): Result<LockStatus> {
        return try {
            val snapshot = database.child("$devicePath/status").get().await()

            if (snapshot.exists()) {
                val locked = snapshot.child("locked").getValue(Boolean::class.java) ?: false
                val lockDuration = snapshot.child("lockDuration").getValue(Long::class.java) ?: 0L
                val lockStartTime = snapshot.child("lockStartTime").getValue(Long::class.java) ?: 0L

                val status = LockStatus(
                    locked = locked,
                    lockDuration = lockDuration,
                    lockStartTime = lockStartTime,
                    paired = true
                )

                Result.success(status)
            } else {
                Result.failure(Exception("No status data found"))
            }
        } catch (e: Exception) {
            Log.e(TAG, "Error getting status from Firebase", e)
            Result.failure(e)
        }
    }

    /**
     * Sends lock command via Firebase
     */
    suspend fun sendLockCommand(): Result<Unit> {
        return sendCommand("lock", null)
    }

    /**
     * Sends unlock command via Firebase
     */
    suspend fun sendUnlockCommand(): Result<Unit> {
        return sendCommand("unlock", null)
    }

    /**
     * Sends a command to the PC via Firebase
     */
    private suspend fun sendCommand(commandType: String, payload: Map<String, Any>?): Result<Unit> {
        return try {
            val commandId = database.child("$devicePath/commands").push().key
                ?: return Result.failure(Exception("Failed to generate command ID"))

            val command = mapOf(
                "type" to commandType,
                "payload" to (payload ?: emptyMap<String, Any>()),
                "timestamp" to System.currentTimeMillis() / 1000,
                "fromDevice" to android.os.Build.MODEL,
                "processed" to false
            )

            database.child("$devicePath/commands/$commandId").setValue(command).await()

            Log.d(TAG, "Command sent: $commandType")
            Result.success(Unit)
        } catch (e: Exception) {
            Log.e(TAG, "Error sending command", e)
            Result.failure(e)
        }
    }

    /**
     * Gets chores list from Firebase
     */
    suspend fun getChores(): Result<List<Chore>> {
        return try {
            val snapshot = database.child("$devicePath/chores").get().await()

            val chores = mutableListOf<Chore>()
            if (snapshot.exists()) {
                for (choreSnapshot in snapshot.children) {
                    val id = choreSnapshot.child("id").getValue(Int::class.java) ?: 0
                    val text = choreSnapshot.child("text").getValue(String::class.java) ?: ""
                    val checked = choreSnapshot.child("checked").getValue(Boolean::class.java) ?: false

                    chores.add(Chore(id, text, checked))
                }
            }

            Result.success(chores)
        } catch (e: Exception) {
            Log.e(TAG, "Error getting chores from Firebase", e)
            Result.failure(e)
        }
    }

    /**
     * Updates chores list in Firebase
     */
    suspend fun updateChores(chores: List<Chore>): Result<Unit> {
        return try {
            val payload = chores.associate { chore ->
                chore.id.toString() to mapOf(
                    "id" to chore.id,
                    "text" to chore.text,
                    "checked" to chore.checked
                )
            }

            sendCommand("updateChores", mapOf("chores" to payload))
        } catch (e: Exception) {
            Log.e(TAG, "Error updating chores in Firebase", e)
            Result.failure(e)
        }
    }

    /**
     * Listens for status updates from Firebase
     */
    fun observeStatus(): Flow<LockStatus> = callbackFlow {
        val listener = object : ValueEventListener {
            override fun onDataChange(snapshot: DataSnapshot) {
                if (snapshot.exists()) {
                    val locked = snapshot.child("locked").getValue(Boolean::class.java) ?: false
                    val lockDuration = snapshot.child("lockDuration").getValue(Long::class.java) ?: 0L
                    val lockStartTime = snapshot.child("lockStartTime").getValue(Long::class.java) ?: 0L

                    val status = LockStatus(
                        locked = locked,
                        lockDuration = lockDuration,
                        lockStartTime = lockStartTime,
                        paired = true
                    )

                    trySend(status)
                }
            }

            override fun onCancelled(error: DatabaseError) {
                Log.e(TAG, "Status observation cancelled", error.toException())
                close(error.toException())
            }
        }

        val statusRef = database.child("$devicePath/status")
        statusRef.addValueEventListener(listener)

        awaitClose {
            statusRef.removeEventListener(listener)
        }
    }

    /**
     * Listens for chore updates from Firebase
     */
    fun observeChores(): Flow<List<Chore>> = callbackFlow {
        val listener = object : ValueEventListener {
            override fun onDataChange(snapshot: DataSnapshot) {
                val chores = mutableListOf<Chore>()
                if (snapshot.exists()) {
                    for (choreSnapshot in snapshot.children) {
                        val id = choreSnapshot.child("id").getValue(Int::class.java) ?: 0
                        val text = choreSnapshot.child("text").getValue(String::class.java) ?: ""
                        val checked = choreSnapshot.child("checked").getValue(Boolean::class.java) ?: false

                        chores.add(Chore(id, text, checked))
                    }
                }
                trySend(chores)
            }

            override fun onCancelled(error: DatabaseError) {
                Log.e(TAG, "Chores observation cancelled", error.toException())
                close(error.toException())
            }
        }

        val choresRef = database.child("$devicePath/chores")
        choresRef.addValueEventListener(listener)

        awaitClose {
            choresRef.removeEventListener(listener)
        }
    }
}
