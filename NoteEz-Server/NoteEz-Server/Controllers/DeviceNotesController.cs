using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoteEz_Server.Models;
using NoteEz_Server.Services;
using System.Security.Claims;

namespace NoteEz_Server.Controllers
{
    // Api/Controllers/DeviceNotesController.cs

    [ApiController]
    [Route("api/device-notes")]
    //[Authorize(AuthenticationSchemes = "DeviceApiKey")]
    public class DeviceNotesController : ControllerBase
    {
        private readonly NoteService _notes;
        public DeviceNotesController(NoteService notes) => _notes = notes;

        private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("lite")]
        public async Task<IActionResult> GetAllLite() => Ok(await _notes.GetAllLiteAsync(UserId));

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var note = await _notes.GetByIdAsync(UserId, id);
            return note is null ? NotFound() : Ok(note);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
            => await _notes.DeleteAsync(UserId, id) ? NoContent() : NotFound();
    }
}
