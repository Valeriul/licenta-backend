using Microsoft.AspNetCore.Mvc;
using RasberryAPI.Peripherals;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BackendAPI.Controllers
{
    [ApiController]
    [Route("/[controller]")]
    public class PeripheralController : ControllerBase
    {
        
        [HttpGet("list")]
        public IActionResult ListPeripherals()
        {
            try
            {
                var peripherals = PeripheralManager.Instance.GetAllPeripherals();
                return Ok(peripherals);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing peripherals: {ex.Message}");
                return StatusCode(500, "An error occurred while listing peripherals.");
            }
        }

        [HttpPost("addTemperatureHumiditySensor")]
        public IActionResult AddTemperatureHumiditySensor()
        {
            string uuid = Guid.NewGuid().ToString(); 

            try
            {
                var config = new PeripheralConfig
                {
                    PeripheralType = "TemperatureHumiditySensor",
                    Uuid = uuid,
                    Url = $"ws://placeholder-temperature-sensor-{uuid}.local"
                };

                PeripheralManager.Instance.AddPeripheral(config);
                return Ok($"TemperatureHumiditySensor with UUID {uuid} added successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding TemperatureHumiditySensor: {ex.Message}");
                return StatusCode(500, "An error occurred while adding the TemperatureHumiditySensor.");
            }
        }

        [HttpPost("addTemperatureControl")]
        public IActionResult AddTemperatureControl()
        {
            string uuid = Guid.NewGuid().ToString(); 

            try
            {
                var config = new PeripheralConfig
                {
                    PeripheralType = "TemperatureControl",
                    Uuid = uuid,
                    Url = $"ws://placeholder-temperature-control-{uuid}.local"
                };

                PeripheralManager.Instance.AddPeripheral(config);
                return Ok($"TemperatureControl with UUID {uuid} added successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding TemperatureControl: {ex.Message}");
                return StatusCode(500, "An error occurred while adding the TemperatureControl.");
            }
        }


        
        [HttpDelete("remove")]
        public IActionResult RemovePeripheral([FromQuery] string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return BadRequest("UUID is required.");
            }

            try
            {
                PeripheralManager.Instance.RemovePeripheral(uuid);
                return Ok("Peripheral removed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing peripheral: {ex.Message}");
                return StatusCode(500, "An error occurred while removing the peripheral.");
            }
        }

        [HttpDelete("removeAll")]

        public IActionResult RemoveAllPeripherals()
        {
            try
            {
                PeripheralManager.Instance.RemoveAllPeripherals();
                return Ok("All peripherals removed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing all peripherals: {ex.Message}");
                return StatusCode(500, "An error occurred while removing all peripherals.");
            }
        }
    }
}