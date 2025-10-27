package com.bl0cked

import android.content.Intent
import android.os.Bundle
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.google.android.material.button.MaterialButton
import com.google.android.material.textfield.TextInputEditText
import kotlinx.coroutines.launch

/**
 * Activity for pairing the Android app with the Windows PC
 */
class PairingActivity : AppCompatActivity() {

    private lateinit var connectionManager: ConnectionManager
    private lateinit var pairingCodeInput: TextInputEditText
    private lateinit var pcIpInput: TextInputEditText
    private lateinit var pairButton: MaterialButton

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_pairing)

        connectionManager = ConnectionManager(this)

        // Check if already paired
        if (connectionManager.isPaired()) {
            navigateToMain()
            return
        }

        setupViews()
    }

    private fun setupViews() {
        pairingCodeInput = findViewById(R.id.pairingCodeInput)
        pcIpInput = findViewById(R.id.pcIpInput)
        pairButton = findViewById(R.id.pairButton)

        pairButton.setOnClickListener {
            attemptPairing()
        }
    }

    private fun attemptPairing() {
        val pairingCode = pairingCodeInput.text?.toString() ?: ""
        val pcIp = pcIpInput.text?.toString() ?: ""

        if (pairingCode.length != 6) {
            Toast.makeText(this, "Pairing code must be 6 digits", Toast.LENGTH_SHORT).show()
            return
        }

        if (pcIp.isEmpty()) {
            Toast.makeText(this, "Please enter PC IP address", Toast.LENGTH_SHORT).show()
            return
        }

        pairButton.isEnabled = false
        pairButton.text = "Pairing..."

        lifecycleScope.launch {
            val result = connectionManager.pairWithPc(pairingCode, pcIp)

            if (result.isSuccess) {
                Toast.makeText(this@PairingActivity, "Pairing successful!", Toast.LENGTH_SHORT).show()
                navigateToMain()
            } else {
                val error = result.exceptionOrNull()?.message ?: "Pairing failed"
                Toast.makeText(this@PairingActivity, error, Toast.LENGTH_LONG).show()
                pairButton.isEnabled = true
                pairButton.text = "Pair"
            }
        }
    }

    private fun navigateToMain() {
        val intent = Intent(this, MainActivity::class.java)
        startActivity(intent)
        finish()
    }
}
