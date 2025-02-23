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
                if (client.IsConnected == true)
                {
                    return await client.SendMessageAndWaitForResponseAsync(message);
                }
                else
                {
                    try
                    {
                        await client.ConnectAsync();
                        return await client.SendMessageAndWaitForResponseAsync(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to connect WebSocket for user {id_user}: {ex.Message}");
                        return null;
                    }
                }
            }
            else
            {
                Console.WriteLine($"WebSocket client not found for user {id_user}. Registering...");
                await AddWebSocketAsync(id_user, message);
                return await SendMessageAsync(id_user, message);
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

        public ConcurrentDictionary<ulong, WebSocketClient> GetAllClients() => _clients;
    }
}
