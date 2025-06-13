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
        public bool IsConnected => _clientWebSocket.State == WebSocketState.Open;

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
            if (_clientWebSocket.State == WebSocketState.Open)
                return;

            if (_clientWebSocket.State == WebSocketState.Closed || _clientWebSocket.State == WebSocketState.Aborted)
            {
                _clientWebSocket.Dispose();
                _clientWebSocket = new ClientWebSocket(); // Recreate WebSocket
            }

            try
            {
                await _clientWebSocket.ConnectAsync(new Uri(WebSocketUri), CancellationToken.None);
                Console.WriteLine($"[INFO] Connected to {WebSocketUri}");
                
                OnConnected?.Invoke(Key); // Notify successful connection
                
                _ = Task.Run(ListenForMessagesAsync);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to connect to WebSocket (User ID: {Key}): {ex.Message}");
                
                OnConnectionFailed?.Invoke(Key); // Notify failed connection
                
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
            while (true)
            {
                await Task.Delay(5000); // Check every 5 seconds

                // Find the oldest pending request
                var oldestRequest = _pendingRequests.Values.OrderBy(x => x.Item2).FirstOrDefault();
                
                if (oldestRequest.Item1 != null)
                {
                    var (_, timestamp) = oldestRequest;
                    if ((DateTime.UtcNow - timestamp).TotalSeconds > 10)
                    {
                        Console.WriteLine($"[WARNING] WebSocket (User ID: {Key}) timeout detected. Removing all pending tasks and closing connection.");

                        // Clear all tasks in dictionary
                        foreach (var kvp in _pendingRequests.ToArray())
                        {
                            if (_pendingRequests.TryRemove(kvp.Key, out var expiredRequest))
                            {
                                var (task, _) = expiredRequest;
                                task.TrySetResult(null);
                            }
                        }

                        // Close WebSocket connection
                        await CloseAsync();

                        return;
                    }
                }
            }
        }

        public async Task CloseAsync()
        {
            if (_clientWebSocket.State == WebSocketState.Open)
            {
                await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection due to timeout", CancellationToken.None);
                OnConnectionFailed?.Invoke(Key); // Notify WebSocket connection failure
                Console.WriteLine($"[INFO] Connection to {WebSocketUri} closed due to timeout.");
            }
        }

        // Helper method to get count of pending requests (for debugging)
        public int GetPendingRequestsCount() => _pendingRequests.Count;
    }
}