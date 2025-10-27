using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Hosting;

namespace Bl0ckedService
{
    /// <summary>
    /// Main orchestration service that coordinates all lockdown components.
    /// Manages overlay windows, keyboard hooks, process monitoring, and communication.
    /// </summary>
    public class LockdownService : IHostedService, IDisposable
    {
        private readonly StateManager _stateManager;
        private readonly PairingManager _pairingManager;
        private readonly KeyboardHook _keyboardHook;
        private readonly ProcessMonitor _processMonitor;
        private readonly HttpServer _httpServer;
        private readonly FirebaseClient _firebaseClient;

        private readonly List<OverlayWindow> _overlayWindows = new List<OverlayWindow>();
        private Thread? _uiThread;
        private bool _isRunning = false;

        public LockdownService()
        {
            _stateManager = new StateManager();
            _pairingManager = new PairingManager(_stateManager);
            _keyboardHook = new KeyboardHook();
            _processMonitor = new ProcessMonitor();
            _httpServer = new HttpServer(_stateManager, _pairingManager);
            _firebaseClient = new FirebaseClient(_stateManager, _pairingManager);

            // Wire up event handlers
            _httpServer.LockStateChanged += OnLockStateChanged;
            _httpServer.ChoresUpdated += OnChoresUpdated;
            _firebaseClient.LockStateChanged += OnLockStateChanged;
            _firebaseClient.ChoresUpdated += OnChoresUpdated;
        }

        /// <summary>
        /// Starts the service and all its components
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Bl0cked Lockdown Service starting...");

            _isRunning = true;

            try
            {
                // Initialize Firebase if credentials exist
                string firebaseCredsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Bl0cked",
                    "firebase-credentials.json"
                );

                if (File.Exists(firebaseCredsPath))
                {
                    _firebaseClient.Initialize(firebaseCredsPath);
                    _firebaseClient.Start(Environment.MachineName);
                }
                else
                {
                    Console.WriteLine("Firebase credentials not found. Internet mode will not be available.");
                }

                // Start HTTP server for local communication
                _httpServer.Start();

                // Install keyboard hook (always installed, enabled when locked)
                _keyboardHook.Install();

                // Check if we should show pairing screen or lockdown screen
                if (!_stateManager.IsPaired)
                {
                    Console.WriteLine("No pairing found. Showing pairing screen...");
                    ShowPairingScreen();
                }
                else if (_stateManager.IsLocked)
                {
                    Console.WriteLine("Restored lock state: LOCKED. Showing overlay...");
                    ActivateLockdown();
                }
                else
                {
                    Console.WriteLine("Restored lock state: UNLOCKED. Running in background...");
                }

                Console.WriteLine("Bl0cked Lockdown Service started successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting service: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the service and cleans up all components
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Bl0cked Lockdown Service stopping...");

            _isRunning = false;

            try
            {
                // Deactivate lockdown if active
                if (_stateManager.IsLocked)
                {
                    DeactivateLockdown();
                }

                // Stop all components
                _httpServer.Stop();
                _firebaseClient.Stop();
                _keyboardHook.Uninstall();
                _processMonitor.Stop();

                // Close overlay windows
                CloseAllOverlays();

                Console.WriteLine("Bl0cked Lockdown Service stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping service: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Shows the pairing screen to pair with Android app
        /// </summary>
        private void ShowPairingScreen()
        {
            // Generate pairing code
            string pairingCode = _pairingManager.GeneratePairingCode();

            Console.WriteLine($"Pairing code generated: {pairingCode}");
            Console.WriteLine("Waiting for Android app to pair...");

            // In a full implementation, this would show a UI window with the code
            // For now, we log it to console
            // The pairing will happen when the Android app calls the /pair endpoint
        }

        /// <summary>
        /// Activates the lockdown (shows overlays, enables hooks, starts process monitoring)
        /// </summary>
        private void ActivateLockdown()
        {
            Console.WriteLine("Activating lockdown...");

            try
            {
                // Enable keyboard hook to block shortcuts
                _keyboardHook.Enable();

                // Start process monitoring to terminate blocked apps
                _processMonitor.Start();

                // Show overlay on all monitors
                ShowOverlaysOnAllMonitors();

                Console.WriteLine("Lockdown activated");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error activating lockdown: {ex.Message}");
            }
        }

        /// <summary>
        /// Deactivates the lockdown (hides overlays, disables hooks, stops process monitoring)
        /// </summary>
        private void DeactivateLockdown()
        {
            Console.WriteLine("Deactivating lockdown...");

            try
            {
                // Disable keyboard hook
                _keyboardHook.Disable();

                // Stop process monitoring
                _processMonitor.Stop();

                // Close all overlay windows
                CloseAllOverlays();

                Console.WriteLine("Lockdown deactivated");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deactivating lockdown: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows overlay windows on all connected monitors
        /// </summary>
        private void ShowOverlaysOnAllMonitors()
        {
            // Ensure we're on a UI thread
            if (_uiThread == null || !_uiThread.IsAlive)
            {
                _uiThread = new Thread(() =>
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    // Create overlay for each screen
                    foreach (Screen screen in Screen.AllScreens)
                    {
                        OverlayWindow overlay = new OverlayWindow(_stateManager, screen);
                        _overlayWindows.Add(overlay);
                        overlay.Show();
                    }

                    // Run message loop
                    Application.Run();
                })
                {
                    IsBackground = false
                };
                _uiThread.SetApartmentState(ApartmentState.STA);
                _uiThread.Start();
            }
        }

        /// <summary>
        /// Closes all overlay windows
        /// </summary>
        private void CloseAllOverlays()
        {
            if (_overlayWindows.Count > 0)
            {
                foreach (var overlay in _overlayWindows)
                {
                    try
                    {
                        if (overlay.InvokeRequired)
                        {
                            overlay.Invoke(new Action(() => overlay.Close()));
                        }
                        else
                        {
                            overlay.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error closing overlay: {ex.Message}");
                    }
                }

                _overlayWindows.Clear();
            }

            // Exit the UI thread's message loop
            if (_uiThread != null && _uiThread.IsAlive)
            {
                try
                {
                    Application.Exit();
                    _uiThread.Join(5000); // Wait up to 5 seconds
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping UI thread: {ex.Message}");
                }

                _uiThread = null;
            }
        }

        /// <summary>
        /// Handles lock state changes from HTTP server or Firebase
        /// </summary>
        private void OnLockStateChanged(object? sender, LockStateChangedEventArgs e)
        {
            Console.WriteLine($"Lock state changed: {(e.Locked ? "LOCKED" : "UNLOCKED")}");

            if (e.Locked)
            {
                ActivateLockdown();
            }
            else
            {
                DeactivateLockdown();
            }
        }

        /// <summary>
        /// Handles chore list updates from HTTP server or Firebase
        /// </summary>
        private void OnChoresUpdated(object? sender, ChoresUpdatedEventArgs e)
        {
            Console.WriteLine("Chores updated - refreshing overlay displays");

            // Reload chores in all overlays
            foreach (var overlay in _overlayWindows)
            {
                try
                {
                    if (overlay.InvokeRequired)
                    {
                        overlay.Invoke(new Action(() => overlay.ReloadChores()));
                    }
                    else
                    {
                        overlay.ReloadChores();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reloading chores in overlay: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            _keyboardHook?.Dispose();
            _processMonitor?.Dispose();
            _httpServer?.Dispose();
            _firebaseClient?.Dispose();

            foreach (var overlay in _overlayWindows)
            {
                overlay?.Dispose();
            }
            _overlayWindows.Clear();

            GC.SuppressFinalize(this);
        }
    }
}
