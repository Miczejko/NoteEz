using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;
using NoteEz_Server.Models;
using NoteEz_Server.Services;
using Xunit;

namespace NoteEz_Server.Tests.Unit
{
    public class NoteServiceTests
    {
        private static AppDbContext NewDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private static NoteService NewNoteService(AppDbContext db)
        {
            // Container jest tylko konstruowany, nie wywolujemy na nim zadnej operacji sieciowej
            // w tych testach (notatki bez zalacznikow audio) - wiec bezpieczny fikcyjny connection string.
            var container = new BlobServiceClient("UseDevelopmentStorage=true").GetBlobContainerClient("test-audio");
            var audioService = new NoteAudioService(db, container);
            return new NoteService(db, audioService);
        }

        // GetByIdAsync i inne odczyty robia .Include(n => n.User) (potrzebne do AuthorUsername) -
        // provider InMemory w odroznieniu od SQL Servera/Postgresa filtruje wyniki, gdy wymagana
        // relacja nie ma odpowiadajacego rekordu, wiec kazdy autor notatki musi realnie istniec.
        private static async Task<Guid> NewUserAsync(AppDbContext db)
        {
            var id = Guid.NewGuid();
            db.Users.Add(new User
            {
                Id = id,
                Username = "user_" + id.ToString("N")[..8],
                Email = id + "@example.com",
                PasswordHash = "x"
            });
            await db.SaveChangesAsync();
            return id;
        }

        [Fact]
        public async Task CreateAsync_PersistsNoteOwnedByCaller()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var userId = await NewUserAsync(db);

            var (dto, _) = await service.CreateAsync(userId, new CreateNoteRequest("Tytul", "tresc", null, null, null));

            var stored = await db.Notes.SingleAsync();
            Assert.Equal(userId, stored.UserId);
            Assert.Equal("Tytul", dto.Title);
        }

        [Fact]
        public async Task GetByIdAsync_OtherUsersNote_ReturnsNull()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var attacker = await NewUserAsync(db);

            var (note, _) = await service.CreateAsync(owner, new CreateNoteRequest("Prywatne", null, null, null, null));

            var asOwner = await service.GetByIdAsync(owner, note.Id);
            var asAttacker = await service.GetByIdAsync(attacker, note.Id);

            Assert.NotNull(asOwner);
            Assert.Null(asAttacker);
        }

        [Fact]
        public async Task UpdateAsync_OtherUsersNote_ReturnsFalseAndDoesNotModify()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var attacker = await NewUserAsync(db);

            var (note, _) = await service.CreateAsync(owner, new CreateNoteRequest("Oryginal", null, null, null, null));

            var (updated, _) = await service.UpdateAsync(attacker, note.Id, new UpdateNoteRequest("Zhakowane", null, null, null, null));

            Assert.Null(updated);
            var stillOriginal = await service.GetByIdAsync(owner, note.Id);
            Assert.Equal("Oryginal", stillOriginal!.Title);
        }

        [Fact]
        public async Task DeleteAsync_OtherUsersNote_ReturnsFalseAndKeepsNote()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var attacker = await NewUserAsync(db);

            var (note, _) = await service.CreateAsync(owner, new CreateNoteRequest("Nie ruszaj", null, null, null, null));

            var deleted = await service.DeleteAsync(attacker, note.Id);

            Assert.False(deleted);
            Assert.NotNull(await service.GetByIdAsync(owner, note.Id));
        }

        [Fact]
        public async Task UpdateAsync_BlankTitle_FallsBackToDefaultInsteadOfEmptyString()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);

            var (note, _) = await service.CreateAsync(owner, new CreateNoteRequest("Cos", null, null, null, null));
            await service.UpdateAsync(owner, note.Id, new UpdateNoteRequest("   ", null, null, null, null));

            var result = await service.GetByIdAsync(owner, note.Id);
            Assert.Equal("Bez tytułu", result!.Title);
        }

        [Fact]
        public async Task GetAllAsync_OnlyReturnsCallersNotes()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var userA = await NewUserAsync(db);
            var userB = await NewUserAsync(db);

            await service.CreateAsync(userA, new CreateNoteRequest("A1", null, null, null, null));
            await service.CreateAsync(userA, new CreateNoteRequest("A2", null, null, null, null));
            await service.CreateAsync(userB, new CreateNoteRequest("B1", null, null, null, null));

            var notesForA = await service.GetAllAsync(userA);

            Assert.Equal(2, notesForA.Count);
            Assert.All(notesForA, n => Assert.NotEqual("B1", n.Title));
        }

        [Fact]
        public async Task GetAllAsync_ExcludesScheduledNotes()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var date = new DateOnly(2026, 7, 29);

            await service.CreateAsync(owner, new CreateNoteRequest("Zwykla", null, null, null, null));
            await service.CreateAsync(owner, new CreateNoteRequest("Kalendarzowa", null, null, date, null));

            var notes = await service.GetAllAsync(owner);

            Assert.Single(notes);
            Assert.Equal("Zwykla", notes[0].Title);
        }

        [Fact]
        public async Task GetAllLiteAsync_ExcludesScheduledNotes()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var date = new DateOnly(2026, 7, 29);

            await service.CreateAsync(owner, new CreateNoteRequest("Zwykla", null, null, null, null));
            await service.CreateAsync(owner, new CreateNoteRequest("Kalendarzowa", null, null, date, null));

            var notes = await service.GetAllLiteAsync(owner);

            Assert.Single(notes);
            Assert.Equal("Zwykla", notes[0].Title);
        }

        [Fact]
        public async Task GetByMonthAsync_ReturnsOnlyNotesForThatMonth()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);

            await service.CreateAsync(owner, new CreateNoteRequest("Lipiec1", null, null, new DateOnly(2026, 7, 5), null));
            await service.CreateAsync(owner, new CreateNoteRequest("Lipiec2", null, null, new DateOnly(2026, 7, 29), null));
            await service.CreateAsync(owner, new CreateNoteRequest("Sierpien", null, null, new DateOnly(2026, 8, 1), null));
            await service.CreateAsync(owner, new CreateNoteRequest("BezDaty", null, null, null, null));

            var julyNotes = await service.GetByMonthAsync(owner, 2026, 7);

            Assert.Equal(2, julyNotes.Count);
            Assert.All(julyNotes, n => Assert.Equal(7, n.ScheduledDate.Month));
        }
    }
}
