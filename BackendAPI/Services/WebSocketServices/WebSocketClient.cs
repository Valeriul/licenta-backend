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
            const int timeoutSeconds = 15; // Increased timeout to 15 seconds
            const int checkIntervalMs = 3000; // Check every 3 seconds

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

                        // Clean up stale requests without closing the connection immediately
                        foreach (var staleRequest in staleRequests)
                        {
                            if (_pendingRequests.TryRemove(staleRequest.Key, out var expiredRequest))
                            {
                                expiredRequest.Item1.TrySetResult(null);
                            }
                        }

                        // Only close connection if we have too many consecutive timeouts
                        if (staleRequests.Count > 3)
                        {
                            Console.WriteLine($"[WARNING] Too many timeouts for user {Key}. Closing connection.");
                            await CloseAsync();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error in timeout cleanup for user {Key}: {ex.Message}");
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
    }
}