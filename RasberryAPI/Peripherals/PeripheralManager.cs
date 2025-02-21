using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using RasberryAPI.Middlewares;

namespace RasberryAPI.Peripherals
{
    public class PeripheralFactory
    {
        public IPeripheral? CreatePeripheral(PeripheralConfig config)
        {
            return config.PeripheralType.ToLower() switch
            {
                "temperaturehumiditysensor" => new TemperatureHumiditySensor(config.Uuid, config.Url),
                "temperaturecontrol" => new TemperatureControl(config.Uuid, config.Url),
                "led" => new LedControl(config.Uuid, config.Url),
                _ => null
            };
        }
    }

    public sealed class PeripheralManager
    {

        private static readonly Lazy<PeripheralManager> _instance = new Lazy<PeripheralManager>(() => new PeripheralManager());
        public static PeripheralManager Instance => _instance.Value;

        private readonly List<IPeripheral> _peripherals;
        private readonly PeripheralFactory _peripheralFactory;

        private readonly string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Peripherals", "peripherals.json");


        private PeripheralManager()
        {
            _peripherals = new List<IPeripheral>();
            _peripheralFactory = new PeripheralFactory();
        }


        public void InitializeFromJson()
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"File not found: {filePath}");
                    return;
                }

                var jsonContent = File.ReadAllText(filePath);
                var peripheralConfigs = JsonConvert.DeserializeObject<List<PeripheralConfig>>(jsonContent);

                foreach (var config in peripheralConfigs)
                {
                    var peripheral = _peripheralFactory.CreatePeripheral(config);
                    if (peripheral != null)
                    {
                        _peripherals.Add(peripheral);
                        Console.WriteLine($"Initialized peripheral: {config.PeripheralType} with UUID {config.Uuid}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing peripherals: {ex.Message}");
            }
        }

        public void AddPeripheral(PeripheralConfig config)
        {
            var peripheral = _peripheralFactory.CreatePeripheral(config);
            if (peripheral != null)
            {
                _peripherals.Add(peripheral);

                var peripheralConfigs = new List<PeripheralConfig>();
                if (File.Exists(filePath))
                {
                    var jsonContent = File.ReadAllText(filePath);
                    peripheralConfigs = JsonConvert.DeserializeObject<List<PeripheralConfig>>(jsonContent);
                }

                peripheralConfigs.Add(config);

                File.WriteAllText(filePath, JsonConvert.SerializeObject(peripheralConfigs, Formatting.Indented));

                Console.WriteLine($"Added peripheral: {config.PeripheralType} with UUID {config.Uuid}");

                NotifyPeripheralAdded(peripheral);
            }
        }

        public void RemovePeripheral(string uuid)
        {
            var peripheral = _peripherals.FirstOrDefault(p => p.UUId == uuid);
            if (peripheral != null)
            {
                _peripherals.Remove(peripheral);

                var peripheralConfigs = new List<PeripheralConfig>();
                if (File.Exists(filePath))
                {
                    var jsonContent = File.ReadAllText(filePath);
                    peripheralConfigs = JsonConvert.DeserializeObject<List<PeripheralConfig>>(jsonContent);
                }

                peripheralConfigs.RemoveAll(p => p.Uuid == uuid);

                File.WriteAllText(filePath, JsonConvert.SerializeObject(peripheralConfigs, Formatting.Indented));

                Console.WriteLine($"Removed peripheral with UUID {uuid}");

                NotifyPeripheralRemoved(uuid);
            }
        }

        public async void NotifyPeripheralAdded(IPeripheral addedPeripheral)
        {

            var message = new
            {
                type = "peripheralAdded",
                data = new
                {
                    uuid = addedPeripheral.UUId,
                    type = addedPeripheral.GetType().Name
                }
            };

            if (WebSocketMiddleware.IsConnected())
            {
                var messageJson = JsonConvert.SerializeObject(message);
                await WebSocketMiddleware.SendMessageAsync(messageJson);
            }
        }

        public async void NotifyPeripheralRemoved(string uuid)
        {

            var message = new
            {
                type = "peripheralRemoved",
                data = new
                {
                    uuid = uuid
                }
            };

            if (WebSocketMiddleware.IsConnected())
            {
                var messageJson = JsonConvert.SerializeObject(message);
                await WebSocketMiddleware.SendMessageAsync(messageJson);
            }
        }


        public async Task<string> HandleRequest(string uuid, string requestData)
        {
            var peripheral = _peripherals.FirstOrDefault(p => p.UUId == uuid);
            if (peripheral != null)
            {
                peripheral.HandleRequest(requestData);
                return peripheral.GetData();
            }

            return string.Empty;
        }

        public async Task RefreshPeripherals(List<PeripheralConfig> peripherals)
        {
            try
            {
                var uuids = peripherals.Select(p => p.Uuid).ToList();
                var existingUuids = _peripherals.Select(p => p.UUId).ToList();

                foreach (var config in peripherals)
                {
                    if (!existingUuids.Contains(config.Uuid))
                    {
                        await Task.Run(() => AddPeripheral(config));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing peripherals: {ex.Message}");
            }
        }

        public IEnumerable<string> GetAllPeripherals()
        {
            if (_peripherals.Count == 0)
            {
                return null;
            }

            var jsonO = _peripherals.Select(p => new
            {
                uuid = p.UUId,
                type = p.GetType().Name
            });

            return new List<string> { JsonConvert.SerializeObject(jsonO) };
        }

        public IEnumerable<string> GetAllData()
        {
            var jsonO = _peripherals.Select(p => new
            {
                uuid = p.UUId,
                data = p.GetData()
            });

            return new List<string> { JsonConvert.SerializeObject(jsonO) };
        }

        public IEnumerable<string> GetAllSensorData()
        {

            var jsonO = _peripherals.Where(p => p is Sensor).Select(p => new
            {
                uuid = p.UUId,
                data = p.GetData()
            });

            return new List<string> { JsonConvert.SerializeObject(jsonO) };
        }



        public void RemoveAllPeripherals()
        {
            for (int i = _peripherals.Count - 1; i >= 0; i--)
            {
                RemovePeripheral(_peripherals[i].UUId);
            }
            File.WriteAllText(filePath, "[]");
        }
    }

    public class PeripheralConfig
    {
        public string PeripheralType { get; set; }
        public string Uuid { get; set; }
        public string Url { get; set; }
    }
}
