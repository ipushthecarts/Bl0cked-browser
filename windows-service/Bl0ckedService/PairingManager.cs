using System;
using System.Security.Cryptography;
using System.Text;

namespace Bl0ckedService
{
    /// <summary>
    /// Manages device pairing between the Windows service and Android app.
    /// Handles 6-digit pairing code generation, validation, and shared secret creation.
    /// </summary>
    public class PairingManager
    {
        private string? _currentPairingCode;
        private DateTime _codeExpiryTime;
        private readonly StateManager _stateManager;
        private readonly object _pairingLock = new object();

        public PairingManager(StateManager stateManager)
        {
            _stateManager = stateManager;
        }

        /// <summary>
        /// Generates a new random 6-digit pairing code that expires in 10 minutes
        /// </summary>
        public string GeneratePairingCode()
        {
            lock (_pairingLock)
            {
                // Generate random 6-digit code
                var random = new Random();
                _currentPairingCode = random.Next(100000, 999999).ToString();

                // Set expiry to 10 minutes from now
                _codeExpiryTime = DateTime.UtcNow.AddMinutes(10);

                return _currentPairingCode;
            }
        }

        /// <summary>
        /// Gets the current pairing code if one exists and hasn't expired
        /// </summary>
        public string? GetCurrentPairingCode()
        {
            lock (_pairingLock)
            {
                if (_currentPairingCode != null && DateTime.UtcNow < _codeExpiryTime)
                {
                    return _currentPairingCode;
                }
                return null;
            }
        }

        /// <summary>
        /// Gets remaining time until current pairing code expires
        /// </summary>
        public TimeSpan GetCodeExpiryRemaining()
        {
            lock (_pairingLock)
            {
                if (_currentPairingCode != null && DateTime.UtcNow < _codeExpiryTime)
                {
                    return _codeExpiryTime - DateTime.UtcNow;
                }
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Checks if the service is already paired with a device
        /// </summary>
        public bool IsAlreadyPaired()
        {
            return _stateManager.IsPaired;
        }

        /// <summary>
        /// Attempts to pair with an Android device using the provided pairing code
        /// </summary>
        /// <param name="providedCode">The 6-digit code from the Android app</param>
        /// <param name="androidDeviceId">Unique identifier for the Android device</param>
        /// <param name="fcmToken">Firebase Cloud Messaging token for push notifications</param>
        /// <returns>PairingResult with success status and shared secret if successful</returns>
        public PairingResult AttemptPairing(string providedCode, string androidDeviceId, string fcmToken)
        {
            lock (_pairingLock)
            {
                // Check if already paired
                if (IsAlreadyPaired())
                {
                    return new PairingResult
                    {
                        Success = false,
                        ErrorMessage = "This PC is already paired. Unpair existing device first."
                    };
                }

                // Check if code exists
                if (string.IsNullOrEmpty(_currentPairingCode))
                {
                    return new PairingResult
                    {
                        Success = false,
                        ErrorMessage = "No pairing code has been generated. Generate a code first."
                    };
                }

                // Check if code has expired
                if (DateTime.UtcNow >= _codeExpiryTime)
                {
                    return new PairingResult
                    {
                        Success = false,
                        ErrorMessage = "Pairing code has expired. Generate a new code."
                    };
                }

                // Validate the provided code
                if (providedCode != _currentPairingCode)
                {
                    return new PairingResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid pairing code. Please check and try again."
                    };
                }

                // Pairing successful - generate shared secret
                string sharedSecret = GenerateSharedSecret();
                string pcIdentifier = GetPcIdentifier();

                // Save pairing data
                _stateManager.SetPairing(androidDeviceId, sharedSecret);

                // Clear the pairing code (can only be used once)
                _currentPairingCode = null;

                return new PairingResult
                {
                    Success = true,
                    SharedSecret = sharedSecret,
                    PcIdentifier = pcIdentifier
                };
            }
        }

        /// <summary>
        /// Generates a cryptographically secure 32-character random shared secret
        /// </summary>
        private string GenerateSharedSecret()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] secretBytes = new byte[24]; // 24 bytes = 32 base64 characters
                rng.GetBytes(secretBytes);
                return Convert.ToBase64String(secretBytes);
            }
        }

        /// <summary>
        /// Gets a unique identifier for this PC
        /// </summary>
        private string GetPcIdentifier()
        {
            // Use machine name and a hash of hardware info as identifier
            string machineName = Environment.MachineName;
            string identifier = $"{machineName}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            return identifier;
        }

        /// <summary>
        /// Clears current pairing and allows re-pairing
        /// Requires admin rights to call
        /// </summary>
        public void ResetPairing()
        {
            lock (_pairingLock)
            {
                _stateManager.ClearPairing();
                _currentPairingCode = null;
            }
        }

        /// <summary>
        /// Validates that a request is from the paired device using the shared secret
        /// </summary>
        public bool ValidateAuthToken(string providedToken)
        {
            if (!_stateManager.IsPaired)
                return false;

            return providedToken == _stateManager.SharedSecret;
        }
    }

    /// <summary>
    /// Result of a pairing attempt
    /// </summary>
    public class PairingResult
    {
        public bool Success { get; set; }
        public string? SharedSecret { get; set; }
        public string? PcIdentifier { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
