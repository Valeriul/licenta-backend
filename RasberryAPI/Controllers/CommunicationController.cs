using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using BackendAPI.Services;
using Newtonsoft.Json;

namespace BackendAPI.Controllers
{
    [ApiController]
    [Route("api/communication")]
    public class CommunicationController : ControllerBase
    {
        [HttpPost("handle-command")]
        public async Task<IActionResult> HandleCommand([FromBody] CommandRequest commandRequest)
        {
            if (commandRequest == null || string.IsNullOrWhiteSpace(commandRequest.CommandType))
            {
                return BadRequest("Invalid request.");
            }

            string? response = await CommunicationManager.Instance.HandleCommand(JsonConvert.SerializeObject(commandRequest));

            if (response == null)
            {
                return NotFound("Command execution failed or returned no data.");
            }

            return Ok(response);
        }
    }
}
