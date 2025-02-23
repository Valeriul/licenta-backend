using Microsoft.AspNetCore.Mvc;
using BackendAPI.Services;
using System.Threading.Tasks;
using BackendAPI.Models; 
using Newtonsoft.Json;

namespace BackendAPI.Controllers
{
    [ApiController]
    [Route("/[controller]")]
    public class PeripheralController : ControllerBase
    {

        private readonly PeripheralService _peripheralService;
        public PeripheralController()
        {
            _peripheralService = PeripheralService.Instance;
        }

        [HttpGet("getLoadingData")]
        public async Task<IActionResult> GetLoadingData([FromQuery] ulong id_user)
        {
            var result = await _peripheralService.GetLoadingData(id_user);
            return Ok(result);
        }

        [HttpPost("makeControlCommand")]
        public async Task<IActionResult> MakeControlCommand([FromBody] BackendAPI.Models.ControlCommand Data, [FromQuery] ulong id_user)
        {
            var result = await _peripheralService.ControlPeripheral(id_user, Data);
            return Ok(result);
        }

        [HttpPost("renamePeripheral")]
        public async Task<IActionResult> RenamePeripheral([FromQuery] ulong id_user, [FromQuery] string uuid, [FromQuery] string newName)
        {
            var result = await _peripheralService.RenamePeripheral(id_user, uuid, newName);
            return Ok(result);
        }

        [HttpPost("relocatePeripheral")]
        public async Task<IActionResult> RelocatePeripheral([FromQuery] ulong id_user, [FromQuery] string uuid, [FromQuery] string newLocation)
        {
            var result = await _peripheralService.RelocatePeripheral(id_user, uuid, newLocation);
            return Ok(result);
        }

        [HttpPost("saveGridPosition")]
        public async Task<IActionResult> SaveGridPosition([FromBody] GridPositionUpdateRequest request)
        {
            var result = await _peripheralService.SaveGridPosition(request);
            return Ok(result);
        }

    }

}
