using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoteEz_Server.Models;
using NoteEz_Server.Services;
using System.Security.Claims;

namespace NoteEz_Server.Controllers
{
    // Api/Controllers/DeviceNotesController.cs

    public record DeviceNoteDto(
        Guid Id,
        string Title,
        string TextContent,
        List<TextBlockDto> TextBlocks,
        DateTime UpdatedAt,
        IReadOnlyList<NoteDrawingDto> Drawings,
        IReadOnlyList<NoteAudioDto> AudioClips
    );

    [ApiController]
    [Route("api/device-notes")]
    [Authorize(AuthenticationSchemes = "DeviceApiKey")]
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
            if (note is null) return NotFound();

            // Notes are edited as TipTap JSON. The device gets a plain-text fallback plus a
            // simplified block list (bold/color/checkbox) it can render without a TipTap parser.
            var plainText = TipTapPlainTextExtractor.Extract(note.TextContent);
            var textBlocks = TipTapBlockExtractor.Extract(note.TextContent);

            return Ok(new DeviceNoteDto(
                note.Id,
                note.Title,
                plainText,
                textBlocks,
                note.UpdatedAt,
                note.Drawings,
                note.AudioClips));
        }

        // Authorize in the future with device API key, but for now, we will allow any device to delete notes
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
            => await _notes.DeleteAsync(UserId, id) ? NoContent() : NotFound();
    }
}
