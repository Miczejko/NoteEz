using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NoteEz_Server.Hubs;
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

    // "checked" jest nullable, zeby brak pola dalo sie odrzucic jawnym 400 (a nie cicho potraktowac jako false).
    public record SetTaskCheckedRequest(bool? Checked);

    // strokesJson to JSON rysunku zapisany jako string (patrz DrawingStrokesValidator); walidacja w serwisie.
    public record AddDeviceDrawingRequest(string? StrokesJson);

    [ApiController]
    [Route("api/device-notes")]
    [Authorize(AuthenticationSchemes = "DeviceApiKey")]
    public class DeviceNotesController : ControllerBase
    {
        // 64 KB rysunku + narzut na escapowanie cudzyslowow w JSON-owym stringu (~1.3x)
        private const long MaxRequestBodyBytes = 100_000;

        private readonly NoteService _notes;
        private readonly IHubContext<AppHub> _hub;

        public DeviceNotesController(NoteService notes, IHubContext<AppHub> hub)
        {
            _notes = notes;
            _hub = hub;
        }

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

        // Idempotentne ustawienie stanu checkboxa (nie toggle): ponowienie tego samego zadania
        // daje ten sam wynik. Zwraca 200 + { "checked": bool } (aktualny stan), zeby urzadzenie
        // moglo go porownac ze stanem narysowanym optymistycznie.
        [HttpPut("{id}/tasks/{index:int}")]
        public async Task<IActionResult> SetTaskChecked(Guid id, int index, [FromBody] SetTaskCheckedRequest req)
        {
            if (req.Checked is not bool isChecked)
                return BadRequest(new { error = "Pole 'checked' (bool) jest wymagane." });

            try
            {
                var (status, groupId) = await _notes.SetTaskCheckedAsync(UserId, id, index, isChecked);
                switch (status)
                {
                    case SetTaskStatus.NotFound:
                        return NotFound();
                    case SetTaskStatus.InvalidContent:
                        return BadRequest(new { error = "Notatka nie ma tresci w formacie edytora (brak checkboxow)." });
                    case SetTaskStatus.IndexOutOfRange:
                        return BadRequest(new { error = "Nieprawidlowy indeks zadania." });
                }

                // Tak jak przy zapisie z aplikacji www: notatki grupowe rozsylaja NoteChanged,
                // zeby otwarty edytor innego uzytkownika pokazal baner o zmianie.
                if (groupId is not null)
                {
                    await _hub.Clients.Group($"group-{groupId}").SendAsync("NoteChanged", new { noteId = id, changeType = "updated" });
                    await _hub.Clients.Group($"note-{id}").SendAsync("NoteChanged", new { noteId = id, changeType = "updated" });
                }

                return Ok(new { @checked = isChecked });
            }
            catch (ConcurrencyConflictException)
            {
                return Conflict(new { error = "Notatka zostala zmieniona przez kogos innego. Sprobuj ponownie." });
            }
        }

        // Dodaje rysunek z urzadzenia do notatki (patrz NoteService.AddDeviceDrawingAsync): zapis
        // NoteDrawing + dopisanie bloku drawingBlock do TipTap JSON. 201 + NoteDrawingDto;
        // 404 cudza/nieistniejaca notatka; 400 niepoprawny rysunek lub przekroczony limit rysunkow;
        // 409 konflikt wersji (urzadzenie moze ponowic recznie).
        [HttpPost("{id}/drawings")]
        [RequestSizeLimit(MaxRequestBodyBytes)]
        public async Task<IActionResult> AddDrawing(Guid id, [FromBody] AddDeviceDrawingRequest req)
        {
            try
            {
                var result = await _notes.AddDeviceDrawingAsync(UserId, id, req.StrokesJson);
                switch (result.Status)
                {
                    case AddDeviceDrawingStatus.NotFound:
                        return NotFound();
                    case AddDeviceDrawingStatus.InvalidDrawing:
                    case AddDeviceDrawingStatus.TooManyDrawings:
                        return BadRequest(new { error = result.Error });
                }

                // Jak przy SetTaskChecked: notatki grupowe rozsylaja NoteChanged (baner w edytorze www).
                if (result.GroupId is not null)
                {
                    await _hub.Clients.Group($"group-{result.GroupId}").SendAsync("NoteChanged", new { noteId = id, changeType = "updated" });
                    await _hub.Clients.Group($"note-{id}").SendAsync("NoteChanged", new { noteId = id, changeType = "updated" });
                }

                return CreatedAtAction(nameof(GetById), new { id }, result.Drawing);
            }
            catch (ConcurrencyConflictException)
            {
                return Conflict(new { error = "Notatka zostala zmieniona przez kogos innego. Sprobuj ponownie." });
            }
        }

        // Authorize in the future with device API key, but for now, we will allow any device to delete notes
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
            => await _notes.DeleteAsync(UserId, id) ? NoContent() : NotFound();
    }
}
