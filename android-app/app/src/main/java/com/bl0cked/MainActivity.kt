package com.bl0cked

import android.content.Intent
import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import androidx.fragment.app.Fragment
import com.google.android.material.bottomnavigation.BottomNavigationView

/**
 * Main activity with bottom navigation for Control and Chores screens
 */
class MainActivity : AppCompatActivity() {

    private lateinit var connectionManager: ConnectionManager
    private lateinit var bottomNav: BottomNavigationView

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        connectionManager = ConnectionManager(this)

        // Check if paired, if not go to pairing
        if (!connectionManager.isPaired()) {
            navigateToPairing()
            return
        }

        setupBottomNavigation()

        // Show control fragment by default
        if (savedInstanceState == null) {
            loadFragment(ControlFragment())
        }
    }

    private fun setupBottomNavigation() {
        bottomNav = findViewById(R.id.bottomNavigation)

        bottomNav.setOnItemSelectedListener { item ->
            when (item.itemId) {
                R.id.nav_control -> {
                    loadFragment(ControlFragment())
                    true
                }
                R.id.nav_chores -> {
                    loadFragment(ChoresFragment())
                    true
                }
                else -> false
            }
        }
    }

    private fun loadFragment(fragment: Fragment) {
        supportFragmentManager.beginTransaction()
            .replace(R.id.fragmentContainer, fragment)
            .commit()
    }

    private fun navigateToPairing() {
        val intent = Intent(this, PairingActivity::class.java)
        startActivity(intent)
        finish()
    }
}
