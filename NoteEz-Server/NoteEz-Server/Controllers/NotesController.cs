using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NoteEz_Server.Hubs;
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
        private readonly IHubContext<AppHub> _hub;

        public NotesController(NoteService notes, NoteAudioService audio, IHubContext<AppHub> hub)
        {
            _notes = notes;
            _audio = audio;
            _hub = hub;
        }

        private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task BroadcastNoteChangedAsync(Guid noteId, Guid? groupId, string changeType)
        {
            if (groupId is null) return;

            await _hub.Clients.Group($"group-{groupId}")
                .SendAsync("NoteChanged", new { noteId, changeType });
            await _hub.Clients.Group($"note-{noteId}")
                .SendAsync("NoteChanged", new { noteId, changeType });
        }

        // --- Notatka: tekst/tytuł ---

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] Guid? groupId)
        {
            try
            {
                return Ok(await _notes.GetAllAsync(UserId, groupId));
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpGet("calendar")]
        public async Task<IActionResult> GetByMonth([FromQuery] int year, [FromQuery] int month, [FromQuery] Guid? groupId)
        {
            if (month < 1 || month > 12) return BadRequest(new { error = "Nieprawidłowy miesiąc." });
            try
            {
                return Ok(await _notes.GetByMonthAsync(UserId, year, month, groupId));
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid? groupId)
        {
            try
            {
                var note = await _notes.GetByIdAsync(UserId, id, groupId);
                return note is null ? NotFound() : Ok(note);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateNoteRequest req)
        {
            try
            {
                var note = await _notes.CreateAsync(UserId, req);
                await BroadcastNoteChangedAsync(note.Id, note.GroupId, "created");
                return CreatedAtAction(nameof(GetById), new { id = note.Id }, note);
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, UpdateNoteRequest req)
        {
            try
            {
                var updated = await _notes.UpdateAsync(UserId, id, req);
                if (updated is null) return NotFound();

                await BroadcastNoteChangedAsync(id, updated.GroupId, "updated");
                return Ok(updated);
            }
            catch (ConcurrencyConflictException)
            {
                return Conflict(new { error = "Notatka została zmieniona przez kogoś innego. Odśwież i spróbuj ponownie." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var groupId = await _notes.GetGroupIdAsync(id);

            var deleted = await _notes.DeleteAsync(UserId, id);
            if (!deleted) return NotFound();

            await BroadcastNoteChangedAsync(id, groupId, "deleted");
            return NoContent();
        }

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

                // Content-Type z formularza to tylko deklaracja klienta - sprawdzamy tez
                // rzeczywisty naglowek pliku, zeby nie przyjac np. .html/.exe podszywajacego
                // sie pod audio/webm.
                var header = new byte[AudioSignatureValidator.RequiredHeaderBytes];
                var read = await stream.ReadAsync(header.AsMemory(0, header.Length));
                stream.Position = 0;

                if (read < 4 || !AudioSignatureValidator.IsRecognizedAudioContainer(header.AsSpan(0, read)))
                    return BadRequest(new { error = "Zawartość pliku nie wygląda na obsługiwane audio." });

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
