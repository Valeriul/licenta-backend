using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BackendAPI.Services
{
    public class CommunicationManager
    {
        private static readonly Lazy<CommunicationManager> _instance = new Lazy<CommunicationManager>(() => new CommunicationManager());
        public static CommunicationManager Instance => _instance.Value;

        private CommunicationManager() { }

        
        public async Task HandleUnexpectedMessageAsync(ulong webSocketKey, string message)
        {
            try
            {
                
                var jsonMessage = JObject.Parse(message);

                
                var messageType = jsonMessage["type"]?.ToString();

                switch (messageType)
                {
                    case "peripheralAdded":
                        
                        var addedPeripheral = jsonMessage["data"];
                        if (addedPeripheral != null)
                        {
                            string uuid = addedPeripheral["uuid"]?.ToString();
                            string peripheralType = addedPeripheral["type"]?.ToString();

                            
                            await MySqlDatabaseService.Instance.ExecuteQueryAsync("INSERT INTO peripherals (uuid_Peripheral, type, uuid_Central) VALUES (@uuid, @type,(SELECT uuid_Central FROM users where id_user = @id_user));", new Dictionary<string, object>
                            {
                                { "@uuid", uuid },
                                { "@type", peripheralType },
                                { "@id_user", webSocketKey }
                            });
                        }
                        break;

                    case "peripheralRemoved":
                        
                        var removedPeripheral = jsonMessage["data"];
                        if (removedPeripheral != null)
                        {
                            string uuid = removedPeripheral["uuid"]?.ToString();

                            await MySqlDatabaseService.Instance.ExecuteQueryAsync("DELETE FROM peripherals WHERE uuid_Peripheral = @uuid AND uuid_Central = (SELECT uuid_Central FROM users where id_user = @id_user);", new Dictionary<string, object>
                            {
                                { "@uuid", uuid },
                                { "@id_user", webSocketKey }
                            });
                        }
                        break;

                    default:
                        Console.WriteLine($"Unknown message type: {messageType}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing message JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling message: {ex.Message}");
            }
        }

        public async Task<string?> HandleCommand(CommandRequest request)
        {
            if (request == null)
            {
                Console.WriteLine("Received null request.");
                return null;
            }
            switch (request.CommandType.ToLower())
            {
                case "add":
                    var convertedWebSocketData = JsonConvert.DeserializeObject<WebSocketRequest>(request.Data);
                    return await WebSocketManager.Instance.AddWebSocketAsync(request.id_user, convertedWebSocketData.Url);
                case "get_all_data":
                    return await WebSocketManager.Instance.SendMessageAsync(request.id_user, "{\"CommandType\":\"GET_ALL_DATA\"}");
                case "get_all_sensor_data":
                    return await WebSocketManager.Instance.SendMessageAsync(request.id_user, "{\"CommandType\":\"GET_ALL_SENSOR_DATA\"}");
                case "get_all_peripherals":
                    return await WebSocketManager.Instance.SendMessageAsync(request.id_user, "{\"CommandType\":\"ALL_PERIPHERALS\"}");
                case "control":
                    return await WebSocketManager.Instance.SendMessageAsync(request.id_user, request.Data);
                default:
                    Console.WriteLine($"Unknown command: {request.CommandType}");
                    return $"Unknown command: {request.CommandType}";
            }
        }
    }


    public class CommandRequest
    {
        public string CommandType { get; set; } = string.Empty;
        public ulong id_user { get; set; } = 0;
        public string Data { get; set; } = string.Empty;
    }

    public class WebSocketRequest
    {
        public string Url { get; set; } = string.Empty;
    }
}
