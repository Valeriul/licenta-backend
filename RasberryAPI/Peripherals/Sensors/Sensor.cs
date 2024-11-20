
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RasberryAPI.Peripherals;

namespace RasberryAPI.Peripherals
{
    public abstract class Sensor : IPeripheral
    {
        public int BatteryLevel { get; set; }
        public string UUId { get; set; }
        public string Url { get; set; }

        public Sensor(string uuid, string url)
        {
            UUId = uuid;
            Url = url;
        }

        public abstract string GetData();
        public abstract void HandleRequest(string request);
    }
}