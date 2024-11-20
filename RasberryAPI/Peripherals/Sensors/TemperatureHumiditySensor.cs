

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RasberryAPI.Peripherals;

namespace RasberryAPI.Peripherals
{
    public class TemperatureHumiditySensor : Sensor
    {
        private static readonly Random random = new Random();

        public TemperatureHumiditySensor(string uuid, string url) : base(uuid, url) { 
            BatteryLevel = random.Next(0, 100);
        }

        public override string GetData()
        {
            var (temperature, humidity) = GenerateRealisticTemperatureHumidity();
            return  "{\"temperature\":" + temperature + ",\"humidity\":" + humidity + ",\"batteryLevel\": " + BatteryLevel + "}";
        }

        public override void HandleRequest(string request)
        {
            
            throw new NotImplementedException();
        }

        private (double Temperature, int Humidity) GenerateRealisticTemperatureHumidity()
        {
            
            double temperatureMin = 18.0; 
            double temperatureMax = 30.0; 

            
            int humidityMin = 30; 
            int humidityMax = 70; 

            
            double temperature = Math.Round(random.NextDouble() * (temperatureMax - temperatureMin) + temperatureMin, 1);

            
            int humidity = random.Next(humidityMin, humidityMax + 1);

            return (temperature, humidity);
        }
    }
}