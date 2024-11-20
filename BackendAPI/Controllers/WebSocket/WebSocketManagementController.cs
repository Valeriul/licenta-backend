using Microsoft.AspNetCore.Mvc;
using BackendAPI.Services;
using System.Threading.Tasks;

namespace WebSocketBackend.Controllers
{
    [ApiController]
    [Route("/[controller]")]
    public class WebSocketManagementController : ControllerBase
    {
        public WebSocketManagementController()
        {
        }

        
        [HttpPost("registerCentralControl")]
        public async Task<IActionResult> AddWebSocket([FromBody] WebSocketRequest request)
        {
            if (string.IsNullOrEmpty(request.url) || request.id_user <= 0)
            {
                return BadRequest("Invalid request data.");
            }

            var response = await BackendAPI.Services.WebSocketManager.Instance.AddWebSocketAsync(request.id_user, request.url);
            return Ok(response);
        }

        
        [HttpGet("listAllOpenWebSockets")]
        public IActionResult ListWebSockets()
        {
            var clients = BackendAPI.Services.WebSocketManager.Instance.GetAllClients();
            return Ok(clients);
        }

        
        public class WebSocketRequest
        {
            public ulong id_user { get; set; } = 0;
            public string url { get; set; } = string.Empty;
        }
    }
}
