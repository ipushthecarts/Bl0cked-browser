package com.bl0cked

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import android.widget.Toast
import androidx.fragment.app.Fragment
import androidx.lifecycle.lifecycleScope
import com.google.android.material.button.MaterialButton
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

/**
 * Fragment for controlling lock/unlock state of the PC
 */
class ControlFragment : Fragment() {

    private lateinit var connectionManager: ConnectionManager
    private lateinit var statusLabel: TextView
    private lateinit var statusValue: TextView
    private lateinit var timerLabel: TextView
    private lateinit var lockButton: MaterialButton

    private var isLocked = false
    private var lockDuration = 0L
    private var isPolling = false

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View? {
        return inflater.inflate(R.layout.fragment_control, container, false)
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        connectionManager = ConnectionManager(requireContext())

        setupViews(view)
        startStatusPolling()
    }

    private fun setupViews(view: View) {
        statusLabel = view.findViewById(R.id.statusLabel)
        statusValue = view.findViewById(R.id.statusValue)
        timerLabel = view.findViewById(R.id.timerLabel)
        lockButton = view.findViewById(R.id.lockButton)

        lockButton.setOnClickListener {
            if (isLocked) {
                sendUnlockCommand()
            } else {
                sendLockCommand()
            }
        }
    }

    private fun startStatusPolling() {
        isPolling = true
        lifecycleScope.launch {
            while (isPolling) {
                fetchStatus()
                delay(2000) // Poll every 2 seconds
            }
        }
    }

    private fun fetchStatus() {
        lifecycleScope.launch {
            val result = connectionManager.getStatus()

            if (result.isSuccess) {
                val status = result.getOrNull()
                if (status != null) {
                    updateUI(status.locked, status.lockDuration)
                }
            } else {
                // Handle error silently or show offline status
                statusValue.text = "OFFLINE"
                timerLabel.visibility = View.GONE
            }
        }
    }

    private fun updateUI(locked: Boolean, duration: Long) {
        isLocked = locked
        lockDuration = duration

        statusValue.text = if (locked) "LOCKED" else "UNLOCKED"

        if (locked) {
            timerLabel.visibility = View.VISIBLE
            timerLabel.text = formatDuration(duration)
            lockButton.text = "DEACTIVATE\nLOCK"
        } else {
            timerLabel.visibility = View.GONE
            lockButton.text = "ACTIVATE\nLOCK"
        }
    }

    private fun formatDuration(seconds: Long): String {
        val hours = seconds / 3600
        val minutes = (seconds % 3600) / 60
        val secs = seconds % 60
        return String.format("Locked for: %02d:%02d:%02d", hours, minutes, secs)
    }

    private fun sendLockCommand() {
        lockButton.isEnabled = false

        lifecycleScope.launch {
            val result = connectionManager.sendLockCommand()

            if (result.isSuccess) {
                Toast.makeText(requireContext(), "Lock activated", Toast.LENGTH_SHORT).show()
                delay(500)
                fetchStatus()
            } else {
                val error = result.exceptionOrNull()?.message ?: "Failed to lock"
                Toast.makeText(requireContext(), error, Toast.LENGTH_LONG).show()
            }

            lockButton.isEnabled = true
        }
    }

    private fun sendUnlockCommand() {
        lockButton.isEnabled = false

        lifecycleScope.launch {
            val result = connectionManager.sendUnlockCommand()

            if (result.isSuccess) {
                Toast.makeText(requireContext(), "Lock deactivated", Toast.LENGTH_SHORT).show()
                delay(500)
                fetchStatus()
            } else {
                val error = result.exceptionOrNull()?.message ?: "Failed to unlock"
                Toast.makeText(requireContext(), error, Toast.LENGTH_LONG).show()
            }

            lockButton.isEnabled = true
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        isPolling = false
    }
}
