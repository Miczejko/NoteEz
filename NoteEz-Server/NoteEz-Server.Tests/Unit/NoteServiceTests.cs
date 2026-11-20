using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;
using NoteEz_Server.Models;
using NoteEz_Server.Services;
using System.Text.Json;
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
    
        // ---- SetTaskCheckedAsync (przelaczanie checkboxow z ESP32) ----

        // taski: "Kup mleko" (0, niezaznaczony), zagniezdzony "Pelne" (1, niezaznaczony), "Chleb" (2, zaznaczony)
        private const string TasksDoc =
            """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Zażółć"}]},""" +
            """{"type":"taskList","content":[""" +
            """{"type":"taskItem","attrs":{"checked":false},"content":[{"type":"paragraph","content":[{"type":"text","text":"Kup mleko"}]},""" +
            """{"type":"taskList","content":[{"type":"taskItem","attrs":{"checked":false},"content":[{"type":"paragraph","content":[{"type":"text","text":"Pelne"}]}]}]}]},""" +
            """{"type":"taskItem","attrs":{"checked":true},"content":[{"type":"paragraph","content":[{"type":"text","text":"Chleb"}]}]}]}]}""";

        private static async Task<Guid> CreateNoteAsync(NoteService service, Guid owner, string? textContent)
        {
            var (dto, _) = await service.CreateAsync(owner, new CreateNoteRequest("T", textContent, null, null, null));
            return dto.Id;
        }

        private static List<bool?> CheckedStates(string? json) =>
            TipTapBlockExtractor.Extract(json).Where(b => b.Type == "taskItem").Select(b => b.Checked).ToList();

        [Fact]
        public async Task SetTaskCheckedAsync_SetsOnlyTargetTask_AndLeavesRestOfDocumentUntouched()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);

            var (status, _) = await service.SetTaskCheckedAsync(owner, id, 1, true); // zagniezdzony

            Assert.Equal(SetTaskStatus.Ok, status);
            var stored = (await db.Notes.SingleAsync()).TextContent;
            Assert.Equal(new List<bool?> { false, true, true }, CheckedStates(stored));
            // reszta dokumentu bez zmian (w tym polskie znaki, bez \uXXXX): cofniecie tej jednej flagi
            // daje tekst identyczny z oryginalem co do znaku
            Assert.Equal(TasksDoc, TipTapTasks.SetChecked(stored, 1, false).NewJson);
        }

        [Fact]
        public async Task SetTaskCheckedAsync_CanUncheck()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);

            var (status, _) = await service.SetTaskCheckedAsync(owner, id, 2, false);

            Assert.Equal(SetTaskStatus.Ok, status);
            Assert.Equal(new List<bool?> { false, false, false }, CheckedStates((await db.Notes.SingleAsync()).TextContent));
        }

        [Fact]
        public async Task SetTaskCheckedAsync_IsIdempotent()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);

            await service.SetTaskCheckedAsync(owner, id, 0, true);
            var afterFirst = (await db.Notes.SingleAsync()).TextContent;
            var (status, _) = await service.SetTaskCheckedAsync(owner, id, 0, true);

            Assert.Equal(SetTaskStatus.Ok, status);
            Assert.Equal(afterFirst, (await db.Notes.SingleAsync()).TextContent);
            Assert.Equal(new List<bool?> { true, false, true }, CheckedStates(afterFirst));
        }

        [Fact]
        public async Task SetTaskCheckedAsync_RefreshesUpdatedAtAndRowVersion()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);
            var before = await db.Notes.AsNoTracking().SingleAsync();
            await Task.Delay(20);

            await service.SetTaskCheckedAsync(owner, id, 0, true);

            var after = await db.Notes.AsNoTracking().SingleAsync();
            Assert.True(after.UpdatedAt > before.UpdatedAt);
            Assert.NotEqual(before.RowVersion, after.RowVersion);
        }

        [Fact]
        public async Task SetTaskCheckedAsync_OtherUsersNote_ReturnsNotFoundAndDoesNotModify()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var attacker = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);
            var before = await db.Notes.AsNoTracking().SingleAsync();

            var (status, _) = await service.SetTaskCheckedAsync(attacker, id, 0, true);

            Assert.Equal(SetTaskStatus.NotFound, status);
            var after = await db.Notes.AsNoTracking().SingleAsync();
            Assert.Equal(before.TextContent, after.TextContent);
            Assert.Equal(before.RowVersion, after.RowVersion);
        }

        [Fact]
        public async Task SetTaskCheckedAsync_UnknownNote_ReturnsNotFound()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);

            var (status, _) = await service.SetTaskCheckedAsync(owner, Guid.NewGuid(), 0, true);

            Assert.Equal(SetTaskStatus.NotFound, status);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        [InlineData(100)]
        public async Task SetTaskCheckedAsync_IndexOutOfRange_ReturnsStatusAndDoesNotModify(int index)
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);
            var before = await db.Notes.AsNoTracking().SingleAsync();

            var (status, _) = await service.SetTaskCheckedAsync(owner, id, index, true);

            Assert.Equal(SetTaskStatus.IndexOutOfRange, status);
            var after = await db.Notes.AsNoTracking().SingleAsync();
            Assert.Equal(before.TextContent, after.TextContent);
            Assert.Equal(before.RowVersion, after.RowVersion);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("stara zwykla tresc")]
        [InlineData("42")]
        public async Task SetTaskCheckedAsync_NotTipTapContent_ReturnsInvalidContent(string? content)
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, content);

            var (status, _) = await service.SetTaskCheckedAsync(owner, id, 0, true);

            Assert.Equal(SetTaskStatus.InvalidContent, status);
            Assert.Equal(content, (await db.Notes.SingleAsync()).TextContent);
        }

        // ---- AddDeviceDrawingAsync (nowy rysunek z ESP32) ----

        private const string ValidStrokes =
            """{"strokes":[{"color":"#d64545","width":3,"points":[{"x":10,"y":12},{"x":40,"y":55.5}]}],"canvasWidth":320,"canvasHeight":190}""";

        private static string StrokesWith(string strokeJson) => "{\"strokes\":[" + strokeJson + "]}";

        private static JsonElement[] DrawingBlocks(string? textContent) =>
            JsonDocument.Parse(textContent!).RootElement.GetProperty("content").EnumerateArray()
                .Where(n => n.GetProperty("type").GetString() == "drawingBlock").ToArray();

        [Fact]
        public async Task AddDeviceDrawingAsync_AddsDrawingRow_AndReturnsDto()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);

            var result = await service.AddDeviceDrawingAsync(owner, id, ValidStrokes);

            Assert.Equal(AddDeviceDrawingStatus.Ok, result.Status);
            var stored = await db.NoteDrawings.SingleAsync();
            Assert.Equal(id, stored.NoteId);
            Assert.Equal(ValidStrokes, stored.StrokesJson);
            Assert.Equal(0, stored.SortOrder);
            Assert.Equal(stored.Id, result.Drawing!.Id);
            Assert.Equal(ValidStrokes, result.Drawing.StrokesJson);
        }

        [Fact]
        public async Task AddDeviceDrawingAsync_SecondDrawing_GetsNextSortOrder()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);

            await service.AddDeviceDrawingAsync(owner, id, ValidStrokes);
            var second = await service.AddDeviceDrawingAsync(owner, id, ValidStrokes);

            Assert.Equal(1, second.Drawing!.SortOrder);
            Assert.Equal(2, DrawingBlocks((await db.Notes.SingleAsync()).TextContent).Length);
        }

        [Fact]
        public async Task AddDeviceDrawingAsync_AppendsDrawingBlock_AndLeavesRestOfDocumentUntouched()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);

            var result = await service.AddDeviceDrawingAsync(owner, id, ValidStrokes);

            var stored = (await db.Notes.SingleAsync()).TextContent!;
            var content = JsonDocument.Parse(stored).RootElement.GetProperty("content");
            var last = content[content.GetArrayLength() - 1];
            Assert.Equal("drawingBlock", last.GetProperty("type").GetString());
            Assert.Equal(result.Drawing!.Id.ToString(), last.GetProperty("attrs").GetProperty("drawingId").GetString());
            // poza dopisanym blokiem dokument jest identyczny co do znaku (polskie znaki bez \uXXXX)
            var withoutBlock = stored.Replace("," + last.GetRawText(), "");
            Assert.Equal(TasksDoc, withoutBlock);
            Assert.Contains("Zażółć", stored);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AddDeviceDrawingAsync_EmptyContent_CreatesDocumentWithOnlyDrawingBlock(string? content)
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, content);

            var result = await service.AddDeviceDrawingAsync(owner, id, ValidStrokes);

            var stored = (await db.Notes.SingleAsync()).TextContent!;
            var root = JsonDocument.Parse(stored).RootElement;
            Assert.Equal("doc", root.GetProperty("type").GetString());
            var blocks = root.GetProperty("content");
            Assert.Equal(1, blocks.GetArrayLength());
            Assert.Equal(result.Drawing!.Id.ToString(), blocks[0].GetProperty("attrs").GetProperty("drawingId").GetString());
        }

        [Fact]
        public async Task AddDeviceDrawingAsync_LegacyPlainText_IsKeptAsParagraphs()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, "Zażółć gęślą\njaźń");

            await service.AddDeviceDrawingAsync(owner, id, ValidStrokes);

            var stored = (await db.Notes.SingleAsync()).TextContent!;
            Assert.Contains("Zażółć gęślą", stored); // bez \uXXXX
            var content = JsonDocument.Parse(stored).RootElement.GetProperty("content");
            Assert.Equal(3, content.GetArrayLength());
            Assert.Equal("paragraph", content[0].GetProperty("type").GetString());
            Assert.Equal("Zażółć gęślą", content[0].GetProperty("content")[0].GetProperty("text").GetString());
            Assert.Equal("jaźń", content[1].GetProperty("content")[0].GetProperty("text").GetString());
            Assert.Equal("drawingBlock", content[2].GetProperty("type").GetString());
            Assert.Equal("Zażółć gęślą\njaźń", TipTapPlainTextExtractor.Extract(stored).Trim());
        }

        [Fact]
        public async Task AddDeviceDrawingAsync_GeneratedContent_IsParsedByExtractorsWithoutException()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);

            foreach (var original in new[] { null, "", "stary tekst", TasksDoc })
            {
                var id = await CreateNoteAsync(service, owner, original);
                await service.AddDeviceDrawingAsync(owner, id, ValidStrokes);
                var stored = (await db.Notes.SingleAsync(n => n.Id == id)).TextContent;

                var blocks = TipTapBlockExtractor.Extract(stored);
                var plain = TipTapPlainTextExtractor.Extract(stored);

                Assert.NotNull(blocks);
                Assert.NotNull(plain);
                if (original == TasksDoc)
                    Assert.Equal(new List<bool?> { false, false, true }, CheckedStates(stored)); // taski nietkniete
            }
        }

        [Fact]
        public async Task AddDeviceDrawingAsync_RefreshesUpdatedAtAndRowVersion()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);
            var before = await db.Notes.AsNoTracking().SingleAsync();
            await Task.Delay(20);

            await service.AddDeviceDrawingAsync(owner, id, ValidStrokes);

            var after = await db.Notes.AsNoTracking().SingleAsync();
            Assert.True(after.UpdatedAt > before.UpdatedAt);
            Assert.NotEqual(before.RowVersion, after.RowVersion);
        }

        [Fact]
        public async Task AddDeviceDrawingAsync_OtherUsersNote_ReturnsNotFoundAndDoesNotModify()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var attacker = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);
            var before = await db.Notes.AsNoTracking().SingleAsync();

            var result = await service.AddDeviceDrawingAsync(attacker, id, ValidStrokes);

            Assert.Equal(AddDeviceDrawingStatus.NotFound, result.Status);
            Assert.Empty(await db.NoteDrawings.ToListAsync());
            var after = await db.Notes.AsNoTracking().SingleAsync();
            Assert.Equal(before.TextContent, after.TextContent);
            Assert.Equal(before.RowVersion, after.RowVersion);
        }

        [Fact]
        public async Task AddDeviceDrawingAsync_UnknownNote_ReturnsNotFound()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);

            var result = await service.AddDeviceDrawingAsync(owner, Guid.NewGuid(), ValidStrokes);

            Assert.Equal(AddDeviceDrawingStatus.NotFound, result.Status);
        }

        [Fact]
        public async Task AddDeviceDrawingAsync_GroupMember_CanAdd_AndGroupIdIsReturned()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var member = await NewUserAsync(db);
            var groupId = Guid.NewGuid();
            db.Groups.Add(new Group { Id = groupId, Name = "G", CreatedByUserId = owner, CreatedAt = DateTime.UtcNow });
            db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = groupId, UserId = owner });
            db.GroupMembers.Add(new GroupMember { Id = Guid.NewGuid(), GroupId = groupId, UserId = member });
            await db.SaveChangesAsync();
            var (dto, _) = await service.CreateAsync(owner, new CreateNoteRequest("T", TasksDoc, null, null, groupId));

            var result = await service.AddDeviceDrawingAsync(member, dto.Id, ValidStrokes);

            Assert.Equal(AddDeviceDrawingStatus.Ok, result.Status);
            Assert.Equal(groupId, result.GroupId);
        }

        public static IEnumerable<object[]> InvalidStrokes() => new[]
        {
            new object[] { "" },
            new object[] { "to nie json" },
            new object[] { "[]" },
            new object[] { "42" },
            new object[] { """{"canvasWidth":100}""" },                                  // brak strokes
            new object[] { """{"strokes":{}}""" },                                       // strokes nie tablica
            new object[] { """{"strokes":[]}""" },                                       // pusty rysunek
            new object[] { StrokesWith("""{"color":"red","width":3,"points":[{"x":1,"y":1},{"x":2,"y":2}]}""") },      // zly kolor
            new object[] { StrokesWith("""{"color":"#12345","width":3,"points":[{"x":1,"y":1},{"x":2,"y":2}]}""") },   // za krotki kolor
            new object[] { StrokesWith("""{"color":"#000000","width":"3","points":[{"x":1,"y":1},{"x":2,"y":2}]}""") },// width nie liczba
            new object[] { StrokesWith("""{"color":"#000000","width":0,"points":[{"x":1,"y":1},{"x":2,"y":2}]}""") },  // width poza zakresem
            new object[] { StrokesWith("""{"color":"#000000","width":999,"points":[{"x":1,"y":1},{"x":2,"y":2}]}""") },
            new object[] { StrokesWith("""{"color":"#000000","width":3,"points":[{"x":1,"y":1}]}""") },                // 1 punkt
            new object[] { StrokesWith("""{"color":"#000000","width":3,"points":[]}""") },
            new object[] { StrokesWith("""{"color":"#000000","width":3}""") },                                          // brak points
            new object[] { StrokesWith("""{"color":"#000000","width":3,"points":[{"x":1,"y":1},{"x":"2","y":2}]}""") },// x nie liczba
            new object[] { StrokesWith("""{"color":"#000000","width":3,"points":[{"x":1,"y":1},{"x":2}]}""") },        // brak y
            new object[] { StrokesWith("""{"color":"#000000","width":3,"points":[{"x":1,"y":1},{"x":1e9,"y":2}]}""") },// poza zakresem
            new object[] { """{"strokes":[{"color":"#000000","width":3,"points":[{"x":1,"y":1},{"x":2,"y":2}]}],"canvasWidth":"a"}""" },  // canvasWidth nie liczba
            new object[] { StrokesWith("""{"color":"#000000","width":3,"points":[[1,1],[2,2]]}""") },                  // punkty jako tablice
        };

        [Theory]
        [MemberData(nameof(InvalidStrokes))]
        public async Task AddDeviceDrawingAsync_InvalidStrokes_ReturnsInvalidAndDoesNotModify(string strokes)
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);
            var before = await db.Notes.AsNoTracking().SingleAsync();

            var result = await service.AddDeviceDrawingAsync(owner, id, strokes);

            Assert.Equal(AddDeviceDrawingStatus.InvalidDrawing, result.Status);
            Assert.False(string.IsNullOrEmpty(result.Error));
            Assert.Empty(await db.NoteDrawings.ToListAsync());
            var after = await db.Notes.AsNoTracking().SingleAsync();
            Assert.Equal(before.TextContent, after.TextContent);
            Assert.Equal(before.RowVersion, after.RowVersion);
        }

        [Fact]
        public async Task AddDeviceDrawingAsync_NullStrokes_ReturnsInvalid()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);

            var result = await service.AddDeviceDrawingAsync(owner, id, null);

            Assert.Equal(AddDeviceDrawingStatus.InvalidDrawing, result.Status);
        }

        [Fact]
        public void DrawingStrokesValidator_Limits_AreEnforced()
        {
            string Stroke(int points) =>
                """{"color":"#000000","width":2,"points":[""" +
                string.Join(",", Enumerable.Range(0, points).Select(i => $"{{\"x\":{i},\"y\":{i}}}")) + "]}";

            Assert.Null(DrawingStrokesValidator.Validate(StrokesWith(Stroke(DrawingStrokesValidator.MaxPointsPerStroke))));
            Assert.NotNull(DrawingStrokesValidator.Validate(StrokesWith(Stroke(DrawingStrokesValidator.MaxPointsPerStroke + 1))));

            // za duzo linii
            var manyStrokes = StrokesWith(string.Join(",", Enumerable.Repeat(Stroke(2), DrawingStrokesValidator.MaxStrokes + 1)));
            Assert.NotNull(DrawingStrokesValidator.Validate(manyStrokes));

            // za duzo punktow lacznie (kazda linia w limicie, suma ponad)
            var perStroke = DrawingStrokesValidator.MaxPointsPerStroke;
            var totalOver = StrokesWith(string.Join(",", Enumerable.Repeat(Stroke(perStroke), DrawingStrokesValidator.MaxTotalPoints / perStroke + 1)));
            Assert.NotNull(DrawingStrokesValidator.Validate(totalOver));

            // za duzy rozmiar w bajtach (pole canvasWidth jest poprawne, wiec to nie ono blokuje)
            var padded = ValidStrokes + new string(' ', DrawingStrokesValidator.MaxBytes);
            Assert.NotNull(DrawingStrokesValidator.Validate(padded));
        }

        [Fact]
        public async Task AddDeviceDrawingAsync_TooManyDrawings_ReturnsErrorAndDoesNotModify()
        {
            await using var db = NewDb();
            var service = NewNoteService(db);
            var owner = await NewUserAsync(db);
            var id = await CreateNoteAsync(service, owner, TasksDoc);
            for (var i = 0; i < NoteService.MaxDrawingsPerNote; i++)
                db.NoteDrawings.Add(new NoteDrawing { Id = Guid.NewGuid(), NoteId = id, StrokesJson = ValidStrokes, SortOrder = i, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var before = await db.Notes.AsNoTracking().SingleAsync();

            var result = await service.AddDeviceDrawingAsync(owner, id, ValidStrokes);

            Assert.Equal(AddDeviceDrawingStatus.TooManyDrawings, result.Status);
            Assert.Equal(NoteService.MaxDrawingsPerNote, await db.NoteDrawings.CountAsync());
            Assert.Equal(before.TextContent, (await db.Notes.AsNoTracking().SingleAsync()).TextContent);
        }
}
}
