using NoteEz_Server.Models;
using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;

namespace NoteEz_Server.Services
{
    // Application/Services/NoteService.cs

    public class NoteService
    {
        private readonly AppDbContext _db;
        private readonly NoteAudioService _audioService;


        public NoteService(AppDbContext db, NoteAudioService audioService)
        {
            _db = db;
            _audioService = audioService;
        }

        public async Task<NoteDto> CreateAsync(Guid userId, CreateNoteRequest req)
        {
            var note = new Note
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = req.Title,
                TextContent = req.TextContent,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Notes.Add(note);
            await _db.SaveChangesAsync();
            return ToDto(note);
        }

        public async Task<List<NoteDto>> GetAllAsync(Guid userId)
        {
            var notes = await _db.Notes
                .Where(n => n.UserId == userId)
                .Include(n => n.Drawings)
                .Include(n => n.AudioClips)
                .OrderByDescending(n => n.UpdatedAt)
                .ToListAsync();

            return notes.Select(ToDto).ToList();
        }

        public async Task<List<NoteLiteDto>> GetAllLiteAsync(Guid userId)
        {
            return await _db.Notes
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.UpdatedAt)
                .Select(n => new NoteLiteDto(
                    n.Id,
                    n.Title,
                    n.Drawings.Any(),
                    n.AudioClips.Any()))
                .ToListAsync();
        }

        public async Task<NoteDto?> GetByIdAsync(Guid userId, Guid noteId)
        {
            var note = await _db.Notes
                .Include(n => n.Drawings)
                .Include(n => n.AudioClips)
                .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);

            return note is null ? null : ToDto(note);
        }

        public async Task<bool> UpdateAsync(Guid userId, Guid noteId, UpdateNoteRequest req)
        {
            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
            if (note is null) return false;

            if (req.Title is not null) note.Title = req.Title;
            if (req.TextContent is not null) note.TextContent = req.TextContent;
            note.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid noteId)
        {
            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId);
            if (note is null) return false;

            await _audioService.DeleteAllForNoteAsync(noteId); // najpierw pliki z Blob Storage

            _db.Notes.Remove(note); // potem wiersz (cascade skasuje Drawings/AudioClips w SQL)
            await _db.SaveChangesAsync();
            return true;
        }


        public async Task<NoteDrawingDto> AddDrawingAsync(Guid userId, Guid noteId, AddDrawingRequest req)
        {
            var note = await _db.Notes
                .Include(n => n.Drawings)
                .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId)
                ?? throw new KeyNotFoundException("Notatka nie znaleziona");

            var drawing = new NoteDrawing
            {
                Id = Guid.NewGuid(),
                NoteId = noteId,
                StrokesJson = req.StrokesJson,
                SortOrder = note.Drawings.Count,
                CreatedAt = DateTime.UtcNow
            };

            _db.NoteDrawings.Add(drawing);
            note.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return new NoteDrawingDto(drawing.Id, drawing.StrokesJson, drawing.SortOrder);
        }

        public async Task<bool> DeleteDrawingAsync(Guid userId, Guid noteId, Guid drawingId)
        {
            var drawing = await _db.NoteDrawings
                .FirstOrDefaultAsync(d => d.Id == drawingId && d.NoteId == noteId && d.Note.UserId == userId);

            if (drawing is null) return false;

            _db.NoteDrawings.Remove(drawing);
            await _db.SaveChangesAsync();
            return true;
        }

        private static NoteDto ToDto(Note n) => new(
            n.Id,
            n.Title,
            n.TextContent,
            n.UpdatedAt,
            n.Drawings.OrderBy(d => d.SortOrder).Select(d => new NoteDrawingDto(d.Id, d.StrokesJson, d.SortOrder)).ToList(),
            n.AudioClips.OrderBy(a => a.SortOrder).Select(a => new NoteAudioDto(a.Id, a.BlobUrl, a.DurationSeconds, a.SortOrder)).ToList()
        );
    }
}
