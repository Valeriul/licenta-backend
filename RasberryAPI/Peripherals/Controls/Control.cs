

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text;
using RasberryAPI.Peripherals;

namespace RasberryAPI.Peripherals
{
    public abstract class Control : IPeripheral
    {
        public string UUId { get; set; }
        public string Url { get; set; }

        public string State { get; set; }
        public int BatteryLevel { get; set; }

        public Control(string uuid, string url)
        {
            UUId = uuid;
            Url = url;
        }

        public abstract string GetData();

        public abstract void SetState(string state);

        public abstract void HandleRequest(string request);
    }
}