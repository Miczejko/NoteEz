using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoteEz_Server.Models;
using NoteEz_Server.Services;
using System.Security.Claims;

namespace NoteEz_Server.Controllers
{
    // Api/Controllers/NotesController.cs

    [ApiController]
    [Route("api/notes")]
    [Authorize] // JWT
    public class NotesController : ControllerBase
    {
        private readonly NoteService _notes;
        private readonly NoteAudioService _audio;

        public NotesController(NoteService notes, NoteAudioService audio)
        {
            _notes = notes;
            _audio = audio;
        }

        private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // --- Notatka: tekst/tytuł ---

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _notes.GetAllAsync(UserId));

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var note = await _notes.GetByIdAsync(UserId, id);
            return note is null ? NotFound() : Ok(note);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateNoteRequest req)
        {
            var note = await _notes.CreateAsync(UserId, req);
            return CreatedAtAction(nameof(GetById), new { id = note.Id }, note);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, UpdateNoteRequest req)
            => await _notes.UpdateAsync(UserId, id, req) ? NoContent() : NotFound();

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
            => await _notes.DeleteAsync(UserId, id) ? NoContent() : NotFound();

        // --- Rysunki ---

        [HttpPost("{id}/drawings")]
        public async Task<IActionResult> AddDrawing(Guid id, AddDrawingRequest req)
        {
            try
            {
                return Ok(await _notes.AddDrawingAsync(UserId, id, req));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }
        [HttpPut("{noteId:guid}/drawings/{drawingId:guid}")]
        public async Task<ActionResult<NoteDrawingDto>> UpdateDrawing(
            Guid noteId, Guid drawingId, [FromBody] AddDrawingRequest req)
        {
            try
            {
                var result = await _notes.UpdateDrawingAsync(UserId, noteId, drawingId, req.StrokesJson);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id}/drawings/{drawingId}")]
        public async Task<IActionResult> DeleteDrawing(Guid id, Guid drawingId)
            => await _notes.DeleteDrawingAsync(UserId, id, drawingId) ? NoContent() : NotFound();


        // --- Audio ---

        private static readonly HashSet<string> AllowedAudioContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "audio/webm", "audio/ogg", "audio/opus", "audio/mpeg", "audio/mp4", "audio/wav", "audio/x-wav"
        };
        private const long MaxAudioFileSizeBytes = 20_000_000; // ~20MB, dopasuj do realnych rozmiarów głosówek

        [HttpPost("{id}/audio")]
        [RequestSizeLimit(MaxAudioFileSizeBytes)]
        public async Task<IActionResult> AddAudio(Guid id, IFormFile file, [FromForm] int durationSeconds)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "Brak pliku audio." });

            if (file.Length > MaxAudioFileSizeBytes)
                return BadRequest(new { error = "Plik audio jest za duży." });

            var mimeType = file.ContentType?.Split(';')[0].Trim();
            if (string.IsNullOrWhiteSpace(mimeType) || !AllowedAudioContentTypes.Contains(mimeType))
                return BadRequest(new { error = $"Nieobsługiwany typ pliku: {file.ContentType}" });

            if (durationSeconds <= 0)
                return BadRequest(new { error = "Nieprawidłowy czas trwania nagrania." });

            try
            {
                await using var stream = file.OpenReadStream();
                var dto = await _audio.AddAsync(UserId, id, stream, file.ContentType, file.Length, durationSeconds);
                return Ok(dto);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        // NotesController.cs
        [HttpGet("{id}/audio/{audioId}/stream")]
        public async Task<IActionResult> StreamAudio(Guid id, Guid audioId)
        {
            var result = await _audio.GetAudioStreamAsync(UserId, id, audioId);
            if (result is null) return NotFound();

            return File(result.Value.stream, result.Value.contentType, enableRangeProcessing: true);
        }

        [HttpDelete("{id}/audio/{audioId}")]
        public async Task<IActionResult> DeleteAudio(Guid id, Guid audioId)
            => await _audio.DeleteAsync(UserId, id, audioId) ? NoContent() : NotFound();
    }
}
