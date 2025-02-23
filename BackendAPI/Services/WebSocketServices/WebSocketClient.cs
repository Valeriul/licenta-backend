using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace BackendAPI.Services
{
    public class WebSocketClient
    {
        private ClientWebSocket _clientWebSocket;
        public string WebSocketUri { get; private set; }

        public ulong Key { get; private set; }
        public bool IsConnected => _clientWebSocket.State == WebSocketState.Open;

        private readonly ConcurrentQueue<(TaskCompletionSource<string?>, DateTime)> _responseQueue;

        // Events for connection success and failure
        public event Action<ulong>? OnConnected;
        public event Action<ulong>? OnConnectionFailed;

        public WebSocketClient(ulong key, string url)
        {
            _clientWebSocket = new ClientWebSocket();
            WebSocketUri = url;
            Key = key;
            _responseQueue = new ConcurrentQueue<(TaskCompletionSource<string?>, DateTime)>();

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

            var responseTask = new TaskCompletionSource<string?>();
            _responseQueue.Enqueue((responseTask, DateTime.UtcNow)); // Store task with timestamp

            var messageBytes = Encoding.UTF8.GetBytes(message);
            await _clientWebSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);

            return await responseTask.Task;
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
            if (_responseQueue.TryDequeue(out var item))
            {
                var (responseTask, _) = item;

                if (message == "null")
                {
                    responseTask.SetResult(null);
                }
                else
                {
                    responseTask.SetResult(message);
                }
            }
            else
            {
                await CommunicationManager.Instance.HandleUnexpectedMessageAsync(Key, message);
            }
        }

        private async Task RemoveStaleTasksAndCloseConnection()
        {
            while (true)
            {
                await Task.Delay(5000); // Check every 5 seconds

                if (_responseQueue.TryPeek(out var oldestTask)) // Check only the first task
                {
                    var (_, timestamp) = oldestTask;
                    if ((DateTime.UtcNow - timestamp).TotalSeconds > 10)
                    {
                        Console.WriteLine($"[WARNING] WebSocket (User ID: {Key}) timeout detected. Removing all pending tasks and closing connection.");

                        // Clear all tasks in queue
                        while (_responseQueue.TryDequeue(out var expiredTask))
                        {
                            var (task, _) = expiredTask;
                            task.TrySetResult(null);
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
    }
}
