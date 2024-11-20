using System.Net.WebSockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RasberryAPI.Peripherals;

namespace BackendAPI.Services
{
    public class CommunicationManager
    {
        private static readonly Lazy<CommunicationManager> _instance = new Lazy<CommunicationManager>(() => new CommunicationManager());
        public static CommunicationManager Instance => _instance.Value;

        private CommunicationManager() { }

        public async Task<string?> HandleCommand(string request)
        {
            try
            {
                CommandRequest commandRequest = JsonConvert.DeserializeObject<CommandRequest>(request);

                if (request == null)
                {
                    Console.WriteLine("Received null request.");
                    return null;
                }
                switch (commandRequest.CommandType.ToUpper())
                {
                    case "ALL_PERIPHERALS":
                        var peripherals = PeripheralManager.Instance.GetAllPeripherals();
                        return await Task.FromResult(JsonConvert.SerializeObject(peripherals));
                    case "GET_ALL_DATA":
                        var data = PeripheralManager.Instance.GetAllData();
                        return await Task.FromResult(JsonConvert.SerializeObject(data));
                    case "GET_ALL_SENSOR_DATA":
                        var sensorData = PeripheralManager.Instance.GetAllSensorData();
                        return await Task.FromResult(JsonConvert.SerializeObject(sensorData));
                    case "CONTROL":
                        return await PeripheralManager.Instance.HandleRequest(commandRequest.Uuid, commandRequest.Data);
                    default:
                        Console.WriteLine("Unknown command type.");
                        return null;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
    }


    public class CommandRequest
    {
        public string CommandType { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public string Uuid { get; set; } = string.Empty;
    }

}
