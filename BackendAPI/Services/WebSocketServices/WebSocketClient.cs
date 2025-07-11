using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace BackendAPI.Services
{
    public class WebSocketClient
    {
        private ClientWebSocket _clientWebSocket;
        public string WebSocketUri { get; private set; }

        public ulong Key { get; private set; }
        public bool IsConnected => _clientWebSocket?.State == WebSocketState.Open;

        public WebSocketState ConnectionState => _clientWebSocket?.State ?? WebSocketState.None;

        // Changed from queue to dictionary for correlation ID matching
        private readonly ConcurrentDictionary<string, (TaskCompletionSource<string?>, DateTime)> _pendingRequests;

        // Track consecutive stale cleanup cycles
        private int _consecutiveStaleCleanups = 0;
        private const int MAX_CONSECUTIVE_STALE_CLEANUPS = 3;

        // Events for connection success and failure
        public event Action<ulong>? OnConnected;
        public event Action<ulong>? OnConnectionFailed;

        public WebSocketClient(ulong key, string url)
        {
            _clientWebSocket = new ClientWebSocket();
            WebSocketUri = url;
            Key = key;
            _pendingRequests = new ConcurrentDictionary<string, (TaskCompletionSource<string?>, DateTime)>();

            // Start a background task to clean up stale tasks
            Task.Run(RemoveStaleTasksAndCloseConnection);
        }

        public async Task ConnectAsync()
        {
            // Check if already connected or connecting
            if (_clientWebSocket.State == WebSocketState.Open ||
                _clientWebSocket.State == WebSocketState.Connecting)
            {
                Console.WriteLine($"[INFO] WebSocket for user {Key} is already connected/connecting. State: {_clientWebSocket.State}");
                return;
            }

            // Dispose and recreate if in a terminal state
            if (_clientWebSocket.State == WebSocketState.Closed ||
                _clientWebSocket.State == WebSocketState.Aborted)
            {
                _clientWebSocket.Dispose();
                _clientWebSocket = new ClientWebSocket();
                Console.WriteLine($"[INFO] Recreated WebSocket for user {Key}");
            }

            try
            {
                Console.WriteLine($"[INFO] Attempting to connect to {WebSocketUri} for user {Key}");
                await _clientWebSocket.ConnectAsync(new Uri(WebSocketUri), CancellationToken.None);
                Console.WriteLine($"[INFO] Successfully connected to {WebSocketUri} for user {Key}");

                // Reset consecutive stale cleanups counter on successful connection
                _consecutiveStaleCleanups = 0;

                OnConnected?.Invoke(Key);
                _ = Task.Run(ListenForMessagesAsync);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to connect WebSocket for user {Key}: {ex.Message}");
                OnConnectionFailed?.Invoke(Key);
                throw;
            }
        }

        public async Task<string?> SendMessageAndWaitForResponseAsync(string message)
        {
            if (_clientWebSocket.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket is not connected.");

            // Generate correlation ID
            var correlationId = Guid.NewGuid().ToString();
            var responseTask = new TaskCompletionSource<string?>();

            // Store pending request with correlation ID
            _pendingRequests[correlationId] = (responseTask, DateTime.UtcNow);

            try
            {
                // Create message with correlation ID
                var messageWithCorrelation = new
                {
                    correlationId = correlationId,
                    data = message,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                var jsonMessage = JsonConvert.SerializeObject(messageWithCorrelation);
                var messageBytes = Encoding.UTF8.GetBytes(jsonMessage);

                await _clientWebSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                //Console.WriteLine($"[DEBUG] Sent message with correlation ID {correlationId}: {message}");

                return await responseTask.Task;
            }
            catch (Exception ex)
            {
                // Clean up on error
                _pendingRequests.TryRemove(correlationId, out _);
                Console.WriteLine($"[ERROR] Failed to send message: {ex.Message}");
                throw;
            }
        }

        private async Task ListenForMessagesAsync()
        {
            while (_clientWebSocket.State == WebSocketState.Open)
            {
                var message = await ReceiveMessageAsync();
                if (message != null)
                {
                    await HandleIncomingMessage(message);
                }
            }
        }

        public async Task<string?> ReceiveMessageAsync()
        {
            var buffer = new byte[1024 * 4];
            var result = await _clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", CancellationToken.None);
                return null;
            }

            return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }

        private async Task HandleIncomingMessage(string message)
        {
            try
            {
                //Console.WriteLine($"[DEBUG] Received message: {message}");

                // Try to parse as JSON with correlation ID
                var messageObject = JsonConvert.DeserializeObject<dynamic>(message);

                if (messageObject?.correlationId != null)
                {
                    // Message with correlation ID
                    string correlationId = messageObject.correlationId.ToString();

                    if (_pendingRequests.TryRemove(correlationId, out var pendingRequest))
                    {
                        var (responseTask, _) = pendingRequest;

                        // Extract response data
                        string responseData = messageObject.data?.ToString();

                        //Console.WriteLine($"[DEBUG] Matched response with correlation ID {correlationId}");

                        // Reset consecutive stale cleanups on successful response
                        if (_consecutiveStaleCleanups > 0)
                        {
                            Console.WriteLine($"[INFO] Successful response received for user {Key}. Resetting consecutive cleanup counter from {_consecutiveStaleCleanups} to 0.");
                            _consecutiveStaleCleanups = 0;
                        }

                        if (responseData == "null" || string.IsNullOrEmpty(responseData))
                        {
                            responseTask.SetResult(null);
                        }
                        else
                        {
                            responseTask.SetResult(responseData);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] No pending request found for correlation ID {correlationId}");
                        // Handle as unexpected message
                        await CommunicationManager.Instance.HandleUnexpectedMessageAsync(Key, message);
                    }
                }
                else
                {
                    // Handle messages without correlation ID (broadcasts, notifications, legacy)
                    Console.WriteLine($"[INFO] Received message without correlation ID: {message}");
                    await CommunicationManager.Instance.HandleUnexpectedMessageAsync(Key, message);
                }
            }
            catch (JsonException)
            {
                // Not JSON, treat as unexpected message
                //Console.WriteLine($"[DEBUG] Received plain text message (treating as unexpected): {message}");
                await CommunicationManager.Instance.HandleUnexpectedMessageAsync(Key, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error handling received message: {ex.Message}");
                await CommunicationManager.Instance.HandleUnexpectedMessageAsync(Key, message);
            }
        }

        private async Task RemoveStaleTasksAndCloseConnection()
        {
            const int timeoutSeconds = 15; // Timeout for individual requests
            const int checkIntervalMs = 5000; // Check every 5 seconds (different from data gathering)
            DateTime lastSuccessfulActivity = DateTime.UtcNow;
            const int healthyActivityTimeoutSeconds = 30; // Consider connection dead if no successful activity for 30 seconds

            while (true)
            {
                try
                {
                    await Task.Delay(checkIntervalMs);

                    // Skip if WebSocket is not connected
                    if (_clientWebSocket?.State != WebSocketState.Open)
                    {
                        continue;
                    }

                    // Find stale requests
                    var staleRequests = _pendingRequests
                        .Where(kvp => (DateTime.UtcNow - kvp.Value.Item2).TotalSeconds > timeoutSeconds)
                        .ToList();

                    if (staleRequests.Any())
                    {
                        Console.WriteLine($"[WARNING] Found {staleRequests.Count} stale requests for user {Key}. Cleaning up...");

                        // Increment consecutive stale cleanups counter
                        _consecutiveStaleCleanups++;

                        // Clean up stale requests
                        foreach (var staleRequest in staleRequests)
                        {
                            if (_pendingRequests.TryRemove(staleRequest.Key, out var expiredRequest))
                            {
                                expiredRequest.Item1.TrySetResult(null);
                            }
                        }

                        // Check if we've had too many consecutive stale cleanups
                        if (_consecutiveStaleCleanups >= MAX_CONSECUTIVE_STALE_CLEANUPS)
                        {
                            Console.WriteLine($"[ERROR] Connection appears dead for user {Key}. " +
                                           $"Had {_consecutiveStaleCleanups} consecutive stale cleanup cycles. " +
                                           $"Closing connection.");
                            await CloseAsync();
                            return;
                        }

                        Console.WriteLine($"[INFO] Consecutive stale cleanups for user {Key}: {_consecutiveStaleCleanups}/{MAX_CONSECUTIVE_STALE_CLEANUPS}");
                    }
                    else
                    {
                        // Update last successful activity time
                        lastSuccessfulActivity = DateTime.UtcNow;
                        
                        // Only reset consecutive counter if we have successful activity AND pending requests were processed
                        if (_consecutiveStaleCleanups > 0 && _pendingRequests.Count == 0)
                        {
                            Console.WriteLine($"[INFO] All requests processed successfully for user {Key}. Resetting consecutive cleanup counter.");
                            _consecutiveStaleCleanups = 0;
                        }
                    }

                    // Additional check: if we haven't had successful activity for too long, consider connection dead
                    var timeSinceLastActivity = (DateTime.UtcNow - lastSuccessfulActivity).TotalSeconds;
                    if (timeSinceLastActivity > healthyActivityTimeoutSeconds && _pendingRequests.Count > 0)
                    {
                        Console.WriteLine($"[ERROR] No successful activity for {timeSinceLastActivity:F1}s for user {Key} with {_pendingRequests.Count} pending requests. Closing connection.");
                        await CloseAsync();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error in timeout cleanup for user {Key}: {ex.Message}");
                    // Don't increment stale counter for cleanup errors
                }
            }
        }

        public async Task CloseAsync()
        {
            try
            {
                // Clear all pending requests first
                foreach (var kvp in _pendingRequests.ToArray())
                {
                    if (_pendingRequests.TryRemove(kvp.Key, out var pendingRequest))
                    {
                        pendingRequest.Item1.TrySetResult(null);
                    }
                }

                if (_clientWebSocket?.State == WebSocketState.Open)
                {
                    await _clientWebSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed",
                        CancellationToken.None);
                }

                Console.WriteLine($"[INFO] WebSocket connection closed for user {Key}");
                OnConnectionFailed?.Invoke(Key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error closing WebSocket for user {Key}: {ex.Message}");
            }
        }

        // Helper method to get count of pending requests (for debugging)
        public int GetPendingRequestsCount() => _pendingRequests.Count;

        // Helper method to get consecutive stale cleanups count (for debugging)
        public int GetConsecutiveStaleCleanups() => _consecutiveStaleCleanups;
    }
}