

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RasberryAPI.Peripherals;
using System.Text;

namespace RasberryAPI.Peripherals
{
    public class LedControl : Control
    {
        private static readonly Random random = new Random();
        public string State { get; set; }

        public LedControl(string uuid, string url) : base(uuid, url)
        {
            BatteryLevel = random.Next(0, 100);
        }


        public override string GetData()
        {
            var endpoint = "http://" + Url + "/module/state?uuid=" + UUId;

            using HttpClient client = new HttpClient();
            var response = client.GetAsync(endpoint).Result;
            var responseData = response.Content.ReadAsStringAsync().Result;


            try{
                string sanitizedResponse = JsonConvert.DeserializeObject<string>(responseData) ?? string.Empty;
                var responseObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(sanitizedResponse);
                State = responseObject["brightness"].ToString();
            }
            catch (Exception e)
            {
                return null;
            }


            return responseData;
        }

        public override void SetState(string state)
        {
            var endpoint = $"http://{Url}/module/state?uuid={UUId}";

            using HttpClient client = new HttpClient();

            var stateDict = new Dictionary<string, string>
            {
                { "state", state }
            };
            var content = new StringContent(JsonConvert.SerializeObject(stateDict), Encoding.UTF8, "application/json");

            var response = client.PostAsync(endpoint, content).Result;

            State = response.Content.ReadAsStringAsync().Result;
        }


        public override void HandleRequest(string request)
        {
            var ledRequest = Newtonsoft.Json.JsonConvert.DeserializeObject<LedRequest>(request);
            switch (ledRequest.type)
            {
                case "SET_BRIGHTNESS":
                    SetState(ledRequest.value);
                    break;
                case "INCREASE_BRIGHTNESS":
                    SetState((int.Parse(State) + 1).ToString());
                    break;
                case "DECREASE_BRIGHTNESS":
                    SetState((int.Parse(State) - 1).ToString());
                    break;
                default:
                    Console.WriteLine("Unknown request type.");
                    break;
            }
        }
    }

    public class LedRequest
    {
        public string type { get; set; }
        public string value { get; set; }
    }
}
