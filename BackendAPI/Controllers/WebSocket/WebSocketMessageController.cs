using Microsoft.AspNetCore.Mvc;
using BackendAPI.Services;

namespace WebSocketBackend.Controllers
{
    [ApiController]
    [Route("/[controller]")]
    public class WebSocketController : ControllerBase
    {

        public WebSocketController() { }

        [HttpPost("sendCommand")]
        public async Task<IActionResult> SendCommand([FromBody] MessageRequest request)
        {
            var result = await CommunicationManager.Instance.HandleCommand(new CommandRequest
            {
                CommandType = request.command,
                id_user = request.id_user,
                Data = request.message
            });

            return Ok(result);
        }
    }

    public class MessageRequest
    {

        public string command { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;

        public ulong id_user { get; set; } = 0;
    }

}
