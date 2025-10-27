using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Bl0ckedService
{
    /// <summary>
    /// Manages persistent state for the lockdown service including lock status,
    /// pairing data, and shared secrets. State is encrypted and stored in
    /// C:\ProgramData\Bl0cked\state.json
    /// </summary>
    public class StateManager
    {
        private static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Bl0cked"
        );

        private static readonly string StateFilePath = Path.Combine(DataDirectory, "state.json");
        private static readonly string ChoresFilePath = Path.Combine(DataDirectory, "chores.json");

        private ServiceState _currentState;
        private readonly object _stateLock = new object();

        public StateManager()
        {
            EnsureDataDirectoryExists();
            LoadState();
        }

        /// <summary>
        /// Ensures the data directory exists with proper permissions
        /// </summary>
        private void EnsureDataDirectoryExists()
        {
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);

                // Set directory permissions to require admin rights
                var dirInfo = new DirectoryInfo(DataDirectory);
                var dirSecurity = dirInfo.GetAccessControl();
                // Note: Full ACL configuration would be added here in production
                dirInfo.SetAccessControl(dirSecurity);
            }
        }

        /// <summary>
        /// Loads state from disk, or creates default state if file doesn't exist
        /// </summary>
        private void LoadState()
        {
            lock (_stateLock)
            {
                if (File.Exists(StateFilePath))
                {
                    try
                    {
                        string encryptedJson = File.ReadAllText(StateFilePath);
                        string decryptedJson = DecryptString(encryptedJson);
                        _currentState = JsonConvert.DeserializeObject<ServiceState>(decryptedJson)
                                        ?? CreateDefaultState();
                    }
                    catch (Exception ex)
                    {
                        // Log error and create default state
                        Console.WriteLine($"Error loading state: {ex.Message}");
                        _currentState = CreateDefaultState();
                    }
                }
                else
                {
                    _currentState = CreateDefaultState();
                    SaveState();
                }
            }
        }

        /// <summary>
        /// Saves current state to disk with encryption
        /// </summary>
        private void SaveState()
        {
            lock (_stateLock)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(_currentState, Formatting.Indented);
                    string encryptedJson = EncryptString(json);
                    File.WriteAllText(StateFilePath, encryptedJson);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving state: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Creates a default state object
        /// </summary>
        private ServiceState CreateDefaultState()
        {
            return new ServiceState
            {
                Locked = false,
                LockStartTime = 0,
                PairedDeviceId = null,
                SharedSecret = null,
                EmergencyUnlockCode = null
            };
        }

        /// <summary>
        /// Encrypts a string using machine-specific key (DPAPI)
        /// </summary>
        private string EncryptString(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                null, // No additional entropy
                DataProtectionScope.LocalMachine // Machine-specific encryption
            );
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Decrypts a string using machine-specific key (DPAPI)
        /// </summary>
        private string DecryptString(string encryptedText)
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.LocalMachine
            );
            return Encoding.UTF8.GetString(plainBytes);
        }

        // Public API methods

        public bool IsLocked
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState.Locked;
                }
            }
        }

        public long LockStartTime
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState.LockStartTime;
                }
            }
        }

        public long LockDuration
        {
            get
            {
                lock (_stateLock)
                {
                    if (!_currentState.Locked || _currentState.LockStartTime == 0)
                        return 0;

                    return DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _currentState.LockStartTime;
                }
            }
        }

        public bool IsPaired
        {
            get
            {
                lock (_stateLock)
                {
                    return !string.IsNullOrEmpty(_currentState.PairedDeviceId)
                           && !string.IsNullOrEmpty(_currentState.SharedSecret);
                }
            }
        }

        public string? PairedDeviceId
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState.PairedDeviceId;
                }
            }
        }

        public string? SharedSecret
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState.SharedSecret;
                }
            }
        }

        public string? EmergencyUnlockCode
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState.EmergencyUnlockCode;
                }
            }
        }

        /// <summary>
        /// Activates the lock and records the start time
        /// </summary>
        public void Lock()
        {
            lock (_stateLock)
            {
                _currentState.Locked = true;
                _currentState.LockStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                SaveState();
            }
        }

        /// <summary>
        /// Deactivates the lock
        /// </summary>
        public void Unlock()
        {
            lock (_stateLock)
            {
                _currentState.Locked = false;
                SaveState();
            }
        }

        /// <summary>
        /// Sets pairing information
        /// </summary>
        public void SetPairing(string deviceId, string sharedSecret)
        {
            lock (_stateLock)
            {
                _currentState.PairedDeviceId = deviceId;
                _currentState.SharedSecret = sharedSecret;
                SaveState();
            }
        }

        /// <summary>
        /// Clears pairing information
        /// </summary>
        public void ClearPairing()
        {
            lock (_stateLock)
            {
                _currentState.PairedDeviceId = null;
                _currentState.SharedSecret = null;
                SaveState();
            }
        }

        /// <summary>
        /// Sets the emergency unlock code
        /// </summary>
        public void SetEmergencyUnlockCode(string code)
        {
            lock (_stateLock)
            {
                _currentState.EmergencyUnlockCode = code;
                SaveState();
            }
        }

        /// <summary>
        /// Validates the emergency unlock code
        /// </summary>
        public bool ValidateEmergencyUnlockCode(string code)
        {
            lock (_stateLock)
            {
                return !string.IsNullOrEmpty(_currentState.EmergencyUnlockCode)
                       && _currentState.EmergencyUnlockCode == code;
            }
        }

        /// <summary>
        /// Gets the path to the chores data file
        /// </summary>
        public string GetChoresFilePath()
        {
            return ChoresFilePath;
        }
    }

    /// <summary>
    /// Represents the persisted service state
    /// </summary>
    public class ServiceState
    {
        public bool Locked { get; set; }
        public long LockStartTime { get; set; }
        public string? PairedDeviceId { get; set; }
        public string? SharedSecret { get; set; }
        public string? EmergencyUnlockCode { get; set; }
    }
}
