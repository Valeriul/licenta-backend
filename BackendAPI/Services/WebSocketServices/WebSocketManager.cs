using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json;
using System.Net.WebSockets;
using BackendAPI.Models;

namespace BackendAPI.Services
{
    public class WebSocketManager
    {
        private static readonly Lazy<WebSocketManager> _instance = new Lazy<WebSocketManager>(() => new WebSocketManager());
        public static WebSocketManager Instance => _instance.Value;



        private readonly ConcurrentDictionary<ulong, WebSocketClient> _clients = new ConcurrentDictionary<ulong, WebSocketClient>();

        // Event handlers for connection success and failure
        public event Action<ulong>? OnConnectionSuccess;
        public event Action<ulong>? OnConnectionFailure;

        public WebSocketManager()
        {
            PeripheralService peripheralService = PeripheralService.Instance;

            OnConnectionSuccess += (id) => PeripheralService.Instance.HandleConnectionSuccess(id);
            OnConnectionFailure += (id) => PeripheralService.Instance.HandleConnectionFailure(id);
        }

        public async Task InitializeAsync()
        {
            if (MySqlDatabaseService.Instance == null)
            {
                throw new InvalidOperationException("MySqlDatabaseService must be initialized before WebSocketManager.");
            }

            var registeredUrls = await MySqlDatabaseService.Instance.ExecuteQueryAsync("SELECT id_user, uuid_Central FROM users");

            foreach (var url in registeredUrls)
            {
                if (url.TryGetValue("id_user", out var idUserObj) &&
                    url.TryGetValue("uuid_Central", out var uuidCentralObj))
                {
                    if (ulong.TryParse(idUserObj?.ToString(), out ulong idUser))
                    {
                        string centralUrl = Encoding.UTF8.GetString(Convert.FromBase64String(uuidCentralObj?.ToString() ?? string.Empty));
                        await AddWebSocketAsync(idUser, centralUrl);
                    }
                }
            }
        }

        public async Task<string> AddWebSocketAsync(ulong id_user, string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return "Invalid WebSocket URL.";
            }

            if (_clients.ContainsKey(id_user))
            {
                return $"WebSocket already exists: {url}";
            }

            var client = new WebSocketClient(id_user, url);

            // Subscribe to the client's connection events
            client.OnConnected += (id) => OnConnectionSuccess?.Invoke(id);
            client.OnConnectionFailed += (id) => OnConnectionFailure?.Invoke(id);

            _clients[id_user] = client;

            try
            {
                await client.ConnectAsync();
                return "WebSocket client registered successfully.";
            }
            catch
            {
                return "Failed to connect WebSocket.";
            }
        }

        public async Task<string?> SendMessageAsync(ulong id_user, string message)
        {
            if (_clients.TryGetValue(id_user, out var client))
            {
                // Check connection state before attempting to send
                if (client.IsConnected)
                {
                    try
                    {
                        return await client.SendMessageAndWaitForResponseAsync(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to send message for user {id_user}: {ex.Message}");
                        // Connection might have dropped, try to reconnect
                        await AttemptReconnection(id_user, client);
                        return null;
                    }
                }
                else
                {
                    // Try to reconnect if not connected
                    return await AttemptReconnection(id_user, client, message);
                }
            }
            else
            {
                Console.WriteLine($"[WARNING] WebSocket client not found for user {id_user}");
                return null;
            }
        }

        public async Task TryReconnectWebSocket(ulong id_user)
        {
            if (_clients.TryGetValue(id_user, out var client))
            {
                try
                {
                    await client.ConnectAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to reconnect WebSocket for user {id_user}: {ex.Message}");
                }
            }
        }

        private async Task<string?> AttemptReconnection(ulong id_user, WebSocketClient client, string message = null)
        {
            try
            {
                Console.WriteLine($"[INFO] Attempting reconnection for user {id_user}");
                await client.ConnectAsync();

                if (message != null && client.IsConnected)
                {
                    return await client.SendMessageAndWaitForResponseAsync(message);
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Reconnection failed for user {id_user}: {ex.Message}");
                return null;
            }
        }

        public ConcurrentDictionary<ulong, WebSocketClient> GetAllClients() => _clients;

        // Add this method to WebSocketManager class

        private readonly ConcurrentDictionary<ulong, int> _dataGatheringFailures = new ConcurrentDictionary<ulong, int>();
        private const int MAX_DATA_GATHERING_FAILURES = 3;

        public void HandleDataGatheringTimeout(ulong id_user)
        {
            var failureCount = _dataGatheringFailures.AddOrUpdate(id_user, 1, (key, oldValue) => oldValue + 1);

            Console.WriteLine($"[WARNING] Data gathering failure {failureCount}/{MAX_DATA_GATHERING_FAILURES} for user {id_user}");

            if (failureCount >= MAX_DATA_GATHERING_FAILURES)
            {
                Console.WriteLine($"[ERROR] Too many data gathering failures for user {id_user}. Forcing WebSocket reconnection.");

                // Remove failure tracking
                _dataGatheringFailures.TryRemove(id_user, out _);

                // Force close and reconnect the WebSocket
                _ = Task.Run(async () =>
                {
                    if (_clients.TryGetValue(id_user, out var client))
                    {
                        await client.CloseAsync();

                        // Wait a bit before reconnecting
                        await Task.Delay(2000);

                        try
                        {
                            await client.ConnectAsync();
                            Console.WriteLine($"[INFO] Successfully reconnected WebSocket for user {id_user} after data gathering failures");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Failed to reconnect WebSocket for user {id_user}: {ex.Message}");
                        }
                    }
                });
            }
        }

        public void ResetDataGatheringFailures(ulong id_user)
        {
            if (_dataGatheringFailures.TryRemove(id_user, out var failureCount))
            {
                Console.WriteLine($"[INFO] Reset data gathering failure count for user {id_user} (was {failureCount})");
            }
        }
    }
}
