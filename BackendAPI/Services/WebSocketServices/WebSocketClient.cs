using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace BackendAPI.Services
{
    public class WebSocketClient
    {
        private readonly ClientWebSocket _clientWebSocket;
        public string WebSocketUri { get; private set; }

        public ulong Key { get; private set; }
        public bool IsConnected => _clientWebSocket.State == WebSocketState.Open;

        private readonly ConcurrentQueue<TaskCompletionSource<string?>> _responseQueue;

        public WebSocketClient(ulong key, string url)
        {
            _clientWebSocket = new ClientWebSocket();
            WebSocketUri = url;
            Key = key;
            _responseQueue = new ConcurrentQueue<TaskCompletionSource<string?>>();
        }

        public async Task ConnectAsync()
        {
            if (_clientWebSocket.State == WebSocketState.Open)
                return;

            try
            {
                await _clientWebSocket.ConnectAsync(new Uri(WebSocketUri), CancellationToken.None);
                Console.WriteLine($"Connected to {WebSocketUri}");
                _ = Task.Run(ListenForMessagesAsync);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to WebSocket: {ex.Message}");
                throw;
            }
        }

        public async Task<string?> SendMessageAndWaitForResponseAsync(string message)
        {
            if (_clientWebSocket.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket is not connected.");

            var responseTask = new TaskCompletionSource<string?>();
            _responseQueue.Enqueue(responseTask);

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
            
            if (_responseQueue.TryDequeue(out var responseTask))
            {
                
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

        public async Task CloseAsync()
        {
            if (_clientWebSocket.State == WebSocketState.Open)
            {
                await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", CancellationToken.None);
                Console.WriteLine($"Connection to {WebSocketUri} closed.");
            }
        }
    }
}
