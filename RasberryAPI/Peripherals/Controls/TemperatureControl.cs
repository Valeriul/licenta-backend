

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RasberryAPI.Peripherals;

namespace RasberryAPI.Peripherals
{
    public class TemperatureControl : Control
    {
        private static readonly Random random = new Random();
    
        public TemperatureControl(string uuid, string url) : base(uuid, url) {
            BatteryLevel = random.Next(0, 100);
            Status = random.Next(18, 30).ToString();
        }

        public override string GetData()
        {
            return "{\"temperature\":" + Status + ",\"batteryLevel\": " + BatteryLevel + "}";
        }

        public override void HandleRequest(string request)
        {
            var temperatureRequest = Newtonsoft.Json.JsonConvert.DeserializeObject<TemperatureRequest>(request);
            switch (temperatureRequest.type)
            {
                case "SET_TEMPERATURE":
                    Status = temperatureRequest.value;
                    break;
                case "INCREASE_TEMPERATURE":
                    Status = (int.Parse(Status) + 1).ToString();
                    break;
                case "DECREASE_TEMPERATURE":
                    Status = (int.Parse(Status) - 1).ToString();
                    break;
                default:
                    Console.WriteLine("Unknown request type.");
                    break;
            }
        }
    }

    public class TemperatureRequest
    {
        public string? type { get; set; }

        public string value { get; set; } = string.Empty;
    }
}