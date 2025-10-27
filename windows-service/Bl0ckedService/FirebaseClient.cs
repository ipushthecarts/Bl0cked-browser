using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;

namespace Bl0ckedService
{
    /// <summary>
    /// Manages Firebase Realtime Database communication for remote control
    /// when the Android app is not on the same local network.
    /// </summary>
    public class FirebaseClient : IDisposable
    {
        private FirebaseApp? _firebaseApp;
        private readonly StateManager _stateManager;
        private readonly PairingManager _pairingManager;
        private string? _pcIdentifier;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning = false;

        public event EventHandler<LockStateChangedEventArgs>? LockStateChanged;
        public event EventHandler<ChoresUpdatedEventArgs>? ChoresUpdated;

        public FirebaseClient(StateManager stateManager, PairingManager pairingManager)
        {
            _stateManager = stateManager;
            _pairingManager = pairingManager;
        }

        /// <summary>
        /// Initializes Firebase with service account credentials
        /// </summary>
        public void Initialize(string serviceAccountJsonPath)
        {
            try
            {
                if (!File.Exists(serviceAccountJsonPath))
                {
                    Console.WriteLine($"Firebase service account file not found: {serviceAccountJsonPath}");
                    return;
                }

                _firebaseApp = FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromFile(serviceAccountJsonPath),
                });

                Console.WriteLine("Firebase initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Firebase: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts listening for Firebase commands and syncing status
        /// </summary>
        public void Start(string pcIdentifier)
        {
            if (_isRunning)
                return;

            _pcIdentifier = pcIdentifier;
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            // Start background tasks
            Task.Run(() => SyncStatusLoop(_cancellationTokenSource.Token));
            Task.Run(() => ListenForCommandsLoop(_cancellationTokenSource.Token));

            Console.WriteLine("Firebase client started");
        }

        /// <summary>
        /// Stops Firebase communication
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            Console.WriteLine("Firebase client stopped");
        }

        /// <summary>
        /// Continuously syncs local status to Firebase
        /// </summary>
        private async Task SyncStatusLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    await SyncStatusToFirebase();
                    await Task.Delay(5000, cancellationToken); // Sync every 5 seconds
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error syncing status to Firebase: {ex.Message}");
                    await Task.Delay(10000, cancellationToken); // Wait longer on error
                }
            }
        }

        /// <summary>
        /// Continuously listens for commands from Firebase
        /// </summary>
        private async Task ListenForCommandsLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    await CheckForCommands();
                    await Task.Delay(2000, cancellationToken); // Check every 2 seconds
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking Firebase commands: {ex.Message}");
                    await Task.Delay(5000, cancellationToken); // Wait longer on error
                }
            }
        }

        /// <summary>
        /// Syncs current status to Firebase
        /// </summary>
        private async Task SyncStatusToFirebase()
        {
            if (_firebaseApp == null || string.IsNullOrEmpty(_pcIdentifier))
                return;

            try
            {
                // In a real implementation, this would use Firebase Realtime Database SDK
                // to write to bl0cked/devices/{pcId}/status/
                // For this code skeleton, we'll simulate the structure

                var statusData = new
                {
                    locked = _stateManager.IsLocked,
                    lockStartTime = _stateManager.LockStartTime,
                    lockDuration = _stateManager.LockDuration,
                    lastUpdate = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                Console.WriteLine($"[Firebase] Syncing status: Locked={statusData.locked}, Duration={statusData.lockDuration}s");

                // TODO: Actual Firebase Database write would go here
                // await database.GetReference($"bl0cked/devices/{_pcIdentifier}/status").SetValueAsync(statusData);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing status to Firebase: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks for new commands from Firebase
        /// </summary>
        private async Task CheckForCommands()
        {
            if (_firebaseApp == null || string.IsNullOrEmpty(_pcIdentifier))
                return;

            try
            {
                // In a real implementation, this would use Firebase Realtime Database SDK
                // to listen for changes to bl0cked/devices/{pcId}/commands/
                // For this code skeleton, we'll simulate the structure

                // TODO: Actual Firebase Database listen would go here
                // var snapshot = await database.GetReference($"bl0cked/devices/{_pcIdentifier}/commands").GetValueAsync();

                // Example of processing a command:
                // if (snapshot.Exists)
                // {
                //     foreach (var commandSnapshot in snapshot.Children)
                //     {
                //         ProcessCommand(commandSnapshot);
                //     }
                // }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking commands from Firebase: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a command received from Firebase
        /// </summary>
        private void ProcessCommand(dynamic commandSnapshot)
        {
            try
            {
                string? commandType = commandSnapshot.Child("type").Value?.ToString();
                string? fromDevice = commandSnapshot.Child("fromDevice").Value?.ToString();
                long timestamp = commandSnapshot.Child("timestamp").Value != null
                    ? Convert.ToInt64(commandSnapshot.Child("timestamp").Value)
                    : 0;
                bool processed = commandSnapshot.Child("processed").Value != null
                    && Convert.ToBoolean(commandSnapshot.Child("processed").Value);

                // Skip if already processed
                if (processed)
                    return;

                // Verify command is from paired device
                if (fromDevice != _stateManager.PairedDeviceId)
                {
                    Console.WriteLine($"Ignoring command from unpaired device: {fromDevice}");
                    return;
                }

                // Check timestamp (reject if older than 30 seconds to prevent replay attacks)
                long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (currentTime - timestamp > 30)
                {
                    Console.WriteLine("Ignoring old command (potential replay attack)");
                    return;
                }

                // Execute command
                switch (commandType?.ToLower())
                {
                    case "lock":
                        Console.WriteLine("[Firebase] Executing LOCK command");
                        _stateManager.Lock();
                        LockStateChanged?.Invoke(this, new LockStateChangedEventArgs { Locked = true });
                        break;

                    case "unlock":
                        Console.WriteLine("[Firebase] Executing UNLOCK command");
                        _stateManager.Unlock();
                        LockStateChanged?.Invoke(this, new LockStateChangedEventArgs { Locked = false });
                        break;

                    case "updatechores":
                        Console.WriteLine("[Firebase] Executing UPDATE_CHORES command");
                        // Extract chores from payload
                        var payload = commandSnapshot.Child("payload").Value;
                        // Process chores update
                        ChoresUpdated?.Invoke(this, new ChoresUpdatedEventArgs());
                        break;

                    default:
                        Console.WriteLine($"Unknown command type: {commandType}");
                        break;
                }

                // Mark command as processed
                // TODO: Update Firebase to mark command as processed
                // await commandSnapshot.Reference.Child("processed").SetValueAsync(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing command: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates chore status in Firebase (called when user checks/unchecks chores)
        /// </summary>
        public async Task SyncChoresToFirebase(List<Chore> chores)
        {
            if (_firebaseApp == null || string.IsNullOrEmpty(_pcIdentifier))
                return;

            try
            {
                Console.WriteLine($"[Firebase] Syncing {chores.Count} chores to Firebase");

                // TODO: Actual Firebase Database write would go here
                // foreach (var chore in chores)
                // {
                //     await database.GetReference($"bl0cked/devices/{_pcIdentifier}/chores/{chore.Id}")
                //         .SetValueAsync(new
                //         {
                //             id = chore.Id,
                //             text = chore.Text,
                //             checked = chore.Checked
                //         });
                // }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing chores to Firebase: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();

            if (_firebaseApp != null)
            {
                _firebaseApp.Delete();
                _firebaseApp = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
