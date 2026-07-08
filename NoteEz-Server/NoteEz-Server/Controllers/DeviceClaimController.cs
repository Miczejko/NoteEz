using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoteEz_Server.Models;
using NoteEz_Server.Services;

namespace NoteEz_Server.Controllers
{
    // Api/Controllers/DeviceClaimController.cs — odbiór klucza przez ESP32, otwarty, chroniony samym kodem
    [ApiController]
    [Route("api/devices")]
    [AllowAnonymous]
    public class DeviceClaimController : ControllerBase
    {
        private readonly DevicePairingService _pairing;
        public DeviceClaimController(DevicePairingService pairing) => _pairing = pairing;

        [HttpPost("claim")]
        public async Task<IActionResult> Claim(ClaimDeviceRequest req)
        {
            try
            {
                return Ok(await _pairing.ClaimAsync(req));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
