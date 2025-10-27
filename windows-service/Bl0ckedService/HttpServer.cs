using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Bl0ckedService
{
    /// <summary>
    /// HTTP server for local communication with the Android app.
    /// Provides REST API endpoints for lock control and chore management.
    /// </summary>
    public class HttpServer : IDisposable
    {
        private const int Port = 45823;
        private HttpListener? _listener;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly StateManager _stateManager;
        private readonly PairingManager _pairingManager;
        private readonly Dictionary<string, DateTime> _rateLimitCache = new Dictionary<string, DateTime>();
        private readonly object _rateLimitLock = new object();

        public event EventHandler<LockStateChangedEventArgs>? LockStateChanged;
        public event EventHandler<ChoresUpdatedEventArgs>? ChoresUpdated;

        public HttpServer(StateManager stateManager, PairingManager pairingManager)
        {
            _stateManager = stateManager;
            _pairingManager = pairingManager;
        }

        /// <summary>
        /// Starts the HTTP server
        /// </summary>
        public void Start()
        {
            if (_listener != null)
                return; // Already started

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{Port}/");

            try
            {
                _listener.Start();
                _cancellationTokenSource = new CancellationTokenSource();

                // Start accepting requests
                Task.Run(() => AcceptRequestsAsync(_cancellationTokenSource.Token));

                Console.WriteLine($"HTTP server started on port {Port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting HTTP server: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stops the HTTP server
        /// </summary>
        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _listener = null;
        }

        /// <summary>
        /// Main loop to accept and handle HTTP requests
        /// </summary>
        private async Task AcceptRequestsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
                }
                catch (HttpListenerException)
                {
                    // Listener stopped, exit loop
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting request: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles an individual HTTP request
        /// </summary>
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                string? clientIp = context.Request.RemoteEndPoint?.Address.ToString();

                // Check rate limiting
                if (clientIp != null && IsRateLimited(clientIp))
                {
                    SendJsonResponse(context.Response, new { error = "Rate limit exceeded" }, HttpStatusCode.TooManyRequests);
                    return;
                }

                string path = context.Request.Url?.AbsolutePath ?? "/";
                string method = context.Request.HttpMethod;

                Console.WriteLine($"Request: {method} {path}");

                // Route requests
                if (path == "/status" && method == "GET")
                {
                    await HandleStatusAsync(context);
                }
                else if (path == "/lock" && method == "POST")
                {
                    await HandleLockAsync(context);
                }
                else if (path == "/unlock" && method == "POST")
                {
                    await HandleUnlockAsync(context);
                }
                else if (path == "/chores" && method == "GET")
                {
                    await HandleGetChoresAsync(context);
                }
                else if (path == "/chores/update" && method == "POST")
                {
                    await HandleUpdateChoresAsync(context);
                }
                else if (path == "/pair" && method == "POST")
                {
                    await HandlePairingAsync(context);
                }
                else
                {
                    SendJsonResponse(context.Response, new { error = "Not found" }, HttpStatusCode.NotFound);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling request: {ex.Message}");
                SendJsonResponse(context.Response, new { error = "Internal server error" }, HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Handles GET /status - Returns current lock status
        /// </summary>
        private async Task HandleStatusAsync(HttpListenerContext context)
        {
            if (!ValidateAuthToken(context))
            {
                SendJsonResponse(context.Response, new { error = "Unauthorized" }, HttpStatusCode.Unauthorized);
                return;
            }

            var response = new
            {
                locked = _stateManager.IsLocked,
                lockDuration = _stateManager.LockDuration,
                paired = _stateManager.IsPaired
            };

            SendJsonResponse(context.Response, response, HttpStatusCode.OK);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Handles POST /lock - Activates lockdown
        /// </summary>
        private async Task HandleLockAsync(HttpListenerContext context)
        {
            string? body = await ReadRequestBodyAsync(context.Request);
            if (body == null)
            {
                SendJsonResponse(context.Response, new { error = "Invalid request body" }, HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                var request = JsonConvert.DeserializeObject<LockRequest>(body);
                if (request == null || !_pairingManager.ValidateAuthToken(request.Token ?? ""))
                {
                    SendJsonResponse(context.Response, new { error = "Unauthorized" }, HttpStatusCode.Unauthorized);
                    return;
                }

                _stateManager.Lock();
                LockStateChanged?.Invoke(this, new LockStateChangedEventArgs { Locked = true });

                var response = new
                {
                    success = true,
                    locked = true,
                    lockStartTime = _stateManager.LockStartTime
                };

                SendJsonResponse(context.Response, response, HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling lock request: {ex.Message}");
                SendJsonResponse(context.Response, new { error = "Failed to lock" }, HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Handles POST /unlock - Deactivates lockdown
        /// </summary>
        private async Task HandleUnlockAsync(HttpListenerContext context)
        {
            string? body = await ReadRequestBodyAsync(context.Request);
            if (body == null)
            {
                SendJsonResponse(context.Response, new { error = "Invalid request body" }, HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                var request = JsonConvert.DeserializeObject<LockRequest>(body);
                if (request == null || !_pairingManager.ValidateAuthToken(request.Token ?? ""))
                {
                    SendJsonResponse(context.Response, new { error = "Unauthorized" }, HttpStatusCode.Unauthorized);
                    return;
                }

                _stateManager.Unlock();
                LockStateChanged?.Invoke(this, new LockStateChangedEventArgs { Locked = false });

                var response = new
                {
                    success = true,
                    locked = false
                };

                SendJsonResponse(context.Response, response, HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling unlock request: {ex.Message}");
                SendJsonResponse(context.Response, new { error = "Failed to unlock" }, HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Handles GET /chores - Returns current chore list
        /// </summary>
        private async Task HandleGetChoresAsync(HttpListenerContext context)
        {
            if (!ValidateAuthToken(context))
            {
                SendJsonResponse(context.Response, new { error = "Unauthorized" }, HttpStatusCode.Unauthorized);
                return;
            }

            try
            {
                string choresPath = _stateManager.GetChoresFilePath();
                if (File.Exists(choresPath))
                {
                    string json = File.ReadAllText(choresPath);
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    context.Response.Close();
                }
                else
                {
                    SendJsonResponse(context.Response, new { chores = new List<object>() }, HttpStatusCode.OK);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting chores: {ex.Message}");
                SendJsonResponse(context.Response, new { error = "Failed to get chores" }, HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Handles POST /chores/update - Updates the chore list
        /// </summary>
        private async Task HandleUpdateChoresAsync(HttpListenerContext context)
        {
            string? body = await ReadRequestBodyAsync(context.Request);
            if (body == null)
            {
                SendJsonResponse(context.Response, new { error = "Invalid request body" }, HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                var request = JsonConvert.DeserializeObject<UpdateChoresRequest>(body);
                if (request == null || !_pairingManager.ValidateAuthToken(request.Token ?? ""))
                {
                    SendJsonResponse(context.Response, new { error = "Unauthorized" }, HttpStatusCode.Unauthorized);
                    return;
                }

                string choresPath = _stateManager.GetChoresFilePath();
                var choreData = new ChoreData { Chores = request.Chores ?? new List<Chore>() };
                string json = JsonConvert.SerializeObject(choreData, Formatting.Indented);
                File.WriteAllText(choresPath, json);

                ChoresUpdated?.Invoke(this, new ChoresUpdatedEventArgs());

                SendJsonResponse(context.Response, new { success = true }, HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating chores: {ex.Message}");
                SendJsonResponse(context.Response, new { error = "Failed to update chores" }, HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Handles POST /pair - Pairing request from Android app
        /// </summary>
        private async Task HandlePairingAsync(HttpListenerContext context)
        {
            string? body = await ReadRequestBodyAsync(context.Request);
            if (body == null)
            {
                SendJsonResponse(context.Response, new { error = "Invalid request body" }, HttpStatusCode.BadRequest);
                return;
            }

            try
            {
                var request = JsonConvert.DeserializeObject<PairingRequest>(body);
                if (request == null)
                {
                    SendJsonResponse(context.Response, new { error = "Invalid pairing request" }, HttpStatusCode.BadRequest);
                    return;
                }

                var result = _pairingManager.AttemptPairing(
                    request.Code ?? "",
                    request.DeviceId ?? "",
                    request.FcmToken ?? ""
                );

                if (result.Success)
                {
                    var response = new
                    {
                        success = true,
                        sharedSecret = result.SharedSecret,
                        pcIdentifier = result.PcIdentifier
                    };
                    SendJsonResponse(context.Response, response, HttpStatusCode.OK);
                }
                else
                {
                    SendJsonResponse(context.Response, new { success = false, error = result.ErrorMessage }, HttpStatusCode.BadRequest);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling pairing: {ex.Message}");
                SendJsonResponse(context.Response, new { error = "Failed to pair" }, HttpStatusCode.InternalServerError);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Validates the auth token from request header
        /// </summary>
        private bool ValidateAuthToken(HttpListenerContext context)
        {
            string? token = context.Request.Headers["X-Auth-Token"];
            return token != null && _pairingManager.ValidateAuthToken(token);
        }

        /// <summary>
        /// Checks if a client IP is rate limited
        /// </summary>
        private bool IsRateLimited(string clientIp)
        {
            lock (_rateLimitLock)
            {
                if (_rateLimitCache.TryGetValue(clientIp, out DateTime lastRequest))
                {
                    // Allow 60 requests per minute = 1 request per second
                    if ((DateTime.UtcNow - lastRequest).TotalSeconds < 1)
                    {
                        return true;
                    }
                }

                _rateLimitCache[clientIp] = DateTime.UtcNow;

                // Clean up old entries
                List<string> toRemove = new List<string>();
                foreach (var kvp in _rateLimitCache)
                {
                    if ((DateTime.UtcNow - kvp.Value).TotalMinutes > 1)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                foreach (string key in toRemove)
                {
                    _rateLimitCache.Remove(key);
                }

                return false;
            }
        }

        /// <summary>
        /// Reads the request body as a string
        /// </summary>
        private async Task<string?> ReadRequestBodyAsync(HttpListenerRequest request)
        {
            if (!request.HasEntityBody)
                return null;

            using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                return await reader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// Sends a JSON response
        /// </summary>
        private void SendJsonResponse(HttpListenerResponse response, object data, HttpStatusCode statusCode)
        {
            response.ContentType = "application/json";
            response.StatusCode = (int)statusCode;

            string json = JsonConvert.SerializeObject(data);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }

    // Request/Response DTOs
    public class LockRequest
    {
        public string? Action { get; set; }
        public string? Token { get; set; }
    }

    public class UpdateChoresRequest
    {
        public List<Chore>? Chores { get; set; }
        public string? Token { get; set; }
    }

    public class PairingRequest
    {
        public string? Code { get; set; }
        public string? DeviceId { get; set; }
        public string? FcmToken { get; set; }
    }

    // Event args
    public class LockStateChangedEventArgs : EventArgs
    {
        public bool Locked { get; set; }
    }

    public class ChoresUpdatedEventArgs : EventArgs
    {
    }
}
