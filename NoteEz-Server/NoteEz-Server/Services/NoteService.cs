using NoteEz_Server.Models;
using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;
using System.Text.RegularExpressions;

namespace NoteEz_Server.Services
{
    // Application/Services/NoteService.cs

    public record MentionNotification(Guid UserId, Notification Notification);

    public class NoteService
    {
        private readonly AppDbContext _db;
        private readonly NoteAudioService _audioService;

        // Te same znaki co dozwolone w nazwie uzytkownika przy rejestracji (RegisterDto).
        private static readonly Regex MentionRegex = new(@"@([A-Za-z0-9_.-]{3,32})", RegexOptions.Compiled);
        private const int MentionThrottleSeconds = 60;

        public NoteService(AppDbContext db, NoteAudioService audioService)
        {
            _db = db;
            _audioService = audioService;
        }

        // Wykrywa @nazwaUzytkownika w tresci notatki i tworzy powiadomienia dla
        // wspomnianych czlonkow grupy - tylko notatki grupowe maja sens do oznaczania,
        // bo prywatna notatka nie ma innych czlonkow z dostepem. Throttlowane per
        // odbiorca (nie per notatka) - max jedno powiadomienie na minute, niezaleznie
        // od tego ile razy w tym czasie ten sam user zostanie oznaczony gdziekolwiek.
        private async Task<List<MentionNotification>> ProcessMentionsAsync(Note note, Guid authorUserId)
        {
            var result = new List<MentionNotification>();
            if (note.GroupId is null) return result;

            var plainText = TipTapPlainTextExtractor.Extract(note.TextContent);
            var usernames = MentionRegex.Matches(plainText)
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (usernames.Count == 0) return result;

            var author = await _db.Users.FirstOrDefaultAsync(u => u.Id == authorUserId);
            var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == note.GroupId);

            var mentionedUsers = await _db.GroupMembers
                .Where(m => m.GroupId == note.GroupId && m.UserId != authorUserId)
                .Include(m => m.User)
                .Where(m => usernames.Contains(m.User.Username))
                .Select(m => m.User)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var user in mentionedUsers)
            {
                var recentlyNotified = await _db.Notifications.AnyAsync(n =>
                    n.UserId == user.Id
                    && n.Type == "Mention"
                    && n.CreatedAt > now.AddSeconds(-MentionThrottleSeconds));
                if (recentlyNotified) continue;

                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Type = "Mention",
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        noteId = note.Id,
                        noteTitle = note.Title,
                        groupId = note.GroupId,
                        groupName = group?.Name,
                        mentionedByUsername = author?.Username
                    }),
                    IsRead = false,
                    CreatedAt = now
                };
                _db.Notifications.Add(notification);
                result.Add(new MentionNotification(user.Id, notification));
            }

            if (result.Count > 0) await _db.SaveChangesAsync();
            return result;
        }

        private async Task EnsureGroupMemberAsync(Guid userId, Guid groupId)
        {
            var isMember = await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("Nie jesteś członkiem tej grupy.");
        }

        public async Task<(NoteDto Note, List<MentionNotification> Mentions)> CreateAsync(Guid userId, CreateNoteRequest req)
        {
            if (req.GroupId.HasValue)
                await EnsureGroupMemberAsync(userId, req.GroupId.Value);

            var note = new Note
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = req.Title,
                TextContent = req.TextContent,
                Color = req.Color,
                ScheduledDate = req.ScheduledDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                GroupId = req.GroupId
            };

            _db.Notes.Add(note);
            await _db.SaveChangesAsync();

            var mentions = await ProcessMentionsAsync(note, userId);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            return (ToDto(note, user?.Username ?? ""), mentions);
        }

        public async Task<List<NoteDto>> GetAllAsync(Guid userId, Guid? groupId = null)
        {
            if (groupId.HasValue)
                await EnsureGroupMemberAsync(userId, groupId.Value);

            var query = groupId.HasValue
                ? _db.Notes.Where(n => n.GroupId == groupId.Value && n.ScheduledDate == null)
                : _db.Notes.Where(n => n.UserId == userId && n.ScheduledDate == null && n.GroupId == null);

            var notes = await query
                .Include(n => n.Drawings)
                .Include(n => n.AudioClips)
                .Include(n => n.User)
                .OrderByDescending(n => n.UpdatedAt)
                .ToListAsync();

            return notes.Select(n => ToDto(n, n.User?.Username ?? "")).ToList();
        }

        public async Task<List<CalendarNoteDto>> GetByMonthAsync(Guid userId, int year, int month, Guid? groupId = null)
        {
            if (groupId.HasValue)
                await EnsureGroupMemberAsync(userId, groupId.Value);

            var query = groupId.HasValue
                ? _db.Notes.Where(n => n.GroupId == groupId.Value)
                : _db.Notes.Where(n => n.UserId == userId && n.GroupId == null);

            return await query
                .Where(n => n.ScheduledDate != null
                    && n.ScheduledDate.Value.Year == year
                    && n.ScheduledDate.Value.Month == month)
                .Include(n => n.User)
                .OrderBy(n => n.ScheduledDate)
                .Select(n => new CalendarNoteDto(n.Id, n.Title, n.TextContent, n.Color, n.ScheduledDate!.Value, n.User.Username, n.GroupId))
                .ToListAsync();
        }

        public async Task<List<NoteLiteDto>> GetAllLiteAsync(Guid userId, Guid? groupId = null)
        {
            if (groupId.HasValue)
                await EnsureGroupMemberAsync(userId, groupId.Value);

            var query = groupId.HasValue
                ? _db.Notes.Where(n => n.GroupId == groupId.Value && n.ScheduledDate == null)
                : _db.Notes.Where(n => n.UserId == userId && n.ScheduledDate == null && n.GroupId == null);

            return await query
                .OrderByDescending(n => n.UpdatedAt)
                .Select(n => new NoteLiteDto(
                    n.Id,
                    n.Title,
                    n.Drawings.Any(),
                    n.AudioClips.Any(),
                    n.Color))
                .ToListAsync();
        }

        public async Task<NoteDto?> GetByIdAsync(Guid userId, Guid noteId, Guid? groupId = null)
        {
            var note = await _db.Notes
                .Include(n => n.Drawings)
                .Include(n => n.AudioClips)
                .Include(n => n.User)
                .FirstOrDefaultAsync(n => n.Id == noteId);

            if (note is null) return null;
            if (!await CanEditAsync(userId, note)) return null;

            return ToDto(note, note.User?.Username ?? "");
        }

        public async Task<Guid?> GetGroupIdAsync(Guid noteId)
        {
            return await _db.Notes.Where(n => n.Id == noteId).Select(n => n.GroupId).FirstOrDefaultAsync();
        }

        private async Task<bool> CanEditAsync(Guid userId, Note note)
        {
            if (note.UserId == userId) return true;
            if (note.GroupId is null) return false;

            return await _db.GroupMembers.AnyAsync(m => m.GroupId == note.GroupId.Value && m.UserId == userId);
        }

        public async Task<(NoteDto? Note, List<MentionNotification> Mentions)> UpdateAsync(Guid userId, Guid noteId, UpdateNoteRequest req)
        {
            var note = await _db.Notes
                .Include(n => n.Drawings)
                .Include(n => n.AudioClips)
                .Include(n => n.User)
                .FirstOrDefaultAsync(n => n.Id == noteId);
            if (note is null) return (null, new List<MentionNotification>());

            if (!await CanEditAsync(userId, note)) return (null, new List<MentionNotification>());

            if (req.Title is not null) note.Title = string.IsNullOrWhiteSpace(req.Title) ? "Bez tytułu" : req.Title;
            if (req.TextContent is not null) note.TextContent = req.TextContent;
            if (req.Color is not null) note.Color = req.Color.Length == 0 ? null : req.Color;
            if (req.ScheduledDate.HasValue) note.ScheduledDate = req.ScheduledDate;
            note.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(req.RowVersionBase64))
            {
                try
                {
                    var rowVersion = Convert.FromBase64String(req.RowVersionBase64);
                    _db.Entry(note).Property(n => n.RowVersion).OriginalValue = rowVersion;
                }
                catch (FormatException)
                {
                    // nieprawidlowy base64 - ignorujemy sprawdzanie wersji, zapis przejdzie bez konfliktu
                }
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException();
            }

            var mentions = await ProcessMentionsAsync(note, userId);
            return (ToDto(note, note.User?.Username ?? ""), mentions);
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid noteId)
        {
            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
            if (note is null) return false;

            if (!await CanEditAsync(userId, note)) return false;

            await _audioService.DeleteAllForNoteAsync(noteId); // najpierw pliki z Blob Storage

            _db.Notes.Remove(note); // potem wiersz (cascade skasuje Drawings/AudioClips w SQL)
            await _db.SaveChangesAsync();
            return true;
        }


        public async Task<NoteDrawingDto> AddDrawingAsync(Guid userId, Guid noteId, AddDrawingRequest req)
        {
            var note = await _db.Notes
                .Include(n => n.Drawings)
                .FirstOrDefaultAsync(n => n.Id == noteId)
                ?? throw new KeyNotFoundException("Notatka nie znaleziona");

            if (!await CanEditAsync(userId, note))
                throw new KeyNotFoundException("Notatka nie znaleziona");

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

        public async Task<NoteDrawingDto> UpdateDrawingAsync(Guid userId, Guid noteId, Guid drawingId, string strokesJson)
        {
            var drawing = await _db.NoteDrawings
                .Include(d => d.Note)
                .FirstOrDefaultAsync(d => d.Id == drawingId && d.NoteId == noteId)
                ?? throw new KeyNotFoundException("Rysunek nie znaleziony");

            if (!await CanEditAsync(userId, drawing.Note))
                throw new KeyNotFoundException("Rysunek nie znaleziony");

            drawing.StrokesJson = strokesJson;
            drawing.Note.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return new NoteDrawingDto(drawing.Id, drawing.StrokesJson, drawing.SortOrder);
        }

        public async Task<bool> DeleteDrawingAsync(Guid userId, Guid noteId, Guid drawingId)
        {
            var drawing = await _db.NoteDrawings
                .Include(d => d.Note)
                .FirstOrDefaultAsync(d => d.Id == drawingId && d.NoteId == noteId);

            if (drawing is null) return false;
            if (!await CanEditAsync(userId, drawing.Note)) return false;

            _db.NoteDrawings.Remove(drawing);
            await _db.SaveChangesAsync();
            return true;
        }

        private static NoteDto ToDto(Note n, string authorUsername) => new(
            n.Id,
            n.Title,
            n.TextContent,
            n.Color,
            n.ScheduledDate,
            n.UpdatedAt,
            n.Drawings.OrderBy(d => d.SortOrder).Select(d => new NoteDrawingDto(d.Id, d.StrokesJson, d.SortOrder)).ToList(),
            n.AudioClips.OrderBy(a => a.SortOrder).Select(a => new NoteAudioDto(a.Id, a.BlobUrl, a.DurationSeconds, a.SortOrder)).ToList(),
            authorUsername,
            n.GroupId,
            Convert.ToBase64String(n.RowVersion)
        );
    }
}
