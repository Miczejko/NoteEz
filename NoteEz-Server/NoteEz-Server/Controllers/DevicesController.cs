using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoteEz_Server.Models;
using NoteEz_Server.Services;
using System.Security.Claims;

namespace NoteEz_Server.Controllers
{
    // Api/Controllers/DevicesController.cs — zarządzanie z poziomu Vue (JWT)
    [ApiController]
    [Route("api/devices")]
    //[Authorize] // JWT
    public class DevicesController : ControllerBase
    {
        private readonly DevicePairingService _pairing;
        public DevicesController(DevicePairingService pairing) => _pairing = pairing;

        private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("pair-init")]
        public async Task<IActionResult> InitPairing() => Ok(await _pairing.InitPairingAsync(UserId));

        [HttpGet]
        public async Task<IActionResult> GetDevices() => Ok(await _pairing.GetUserDevicesAsync(UserId));

        [HttpDelete("{deviceId}")]
        public async Task<IActionResult> Revoke(Guid deviceId)
            => await _pairing.RevokeAsync(UserId, deviceId) ? NoContent() : NotFound();
    }
}
