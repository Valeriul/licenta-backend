using System.Collections.Concurrent;
using System.Text;
using Newtonsoft.Json;
using BackendAPI.Models;

namespace BackendAPI.Services
{
    public class WebSocketManager
    {
        private static readonly Lazy<WebSocketManager> _instance = new Lazy<WebSocketManager>(() => new WebSocketManager());
        public static WebSocketManager Instance => _instance.Value;

        private readonly ConcurrentDictionary<ulong, WebSocketClient> _clients = new ConcurrentDictionary<ulong, WebSocketClient>();

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
            _clients[id_user] = client; 

            try
            {
                await client.ConnectAsync();
                return "WebSocket client registered successfully.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect WebSocket for user {id_user}: {ex.Message}");
                return "Failed to connect WebSocket.";
            }
        }

        
        private async Task RegisterPeripheralsAsync(List<PeripheralRegister> peripherals, string websocketUri)
        {
            foreach (var peripheral in peripherals)
            {
                try
                {
                    await MySqlDatabaseService.Instance.ExecuteQueryAsync(
                        "INSERT INTO peripherals(uuid_Peripheral, type, uuid_Central) VALUES(@uuid_Peripheral, @type, @uuid_Central) " +
                        "ON DUPLICATE KEY UPDATE uuid_Peripheral = @uuid_Peripheral, uuid_Central = @uuid_Central",
                        new Dictionary<string, object>
                        {
                    { "@uuid_Peripheral", peripheral.uuid },
                    { "@type", peripheral.type },
                    { "@uuid_Central", await urlToUuid(websocketUri) }
                        });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error registering peripheral {peripheral.uuid}: {ex.Message}");
                }
            }
        }


        public async Task<string> urlToUuid(string url)
        {
            return await Task.Run(() => Convert.ToBase64String(Encoding.UTF8.GetBytes(url)));
        }


        public async Task<string?> SendMessageAsync(ulong id_user, string message)
        {
            if (_clients.TryGetValue(id_user, out var client))
            {
                
                if (client.IsConnected)
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
                return null;
            }
        }

        public ConcurrentDictionary<ulong, WebSocketClient> GetAllClients() => _clients;
    }
}
