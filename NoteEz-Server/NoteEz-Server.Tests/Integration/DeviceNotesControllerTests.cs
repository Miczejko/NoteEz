using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NoteEz_Server.Tests.Integration
{
    // Testy koncowo-do-konca PUT /api/device-notes/{id}/tasks/{index} z prawdziwym przeplywem
    // parowania urzadzenia: rejestracja -> JWT -> pair-init -> claim (klucz X-Api-Key) -> notatka.
    public class DeviceNotesControllerTests
    {
        private const string TestTurnstileToken = "test-turnstile-token";

        private const string TasksDoc =
            """{"type":"doc","content":[{"type":"taskList","content":[""" +
            """{"type":"taskItem","attrs":{"checked":false},"content":[{"type":"paragraph","content":[{"type":"text","text":"Kup mleko"}]}]},""" +
            """{"type":"taskItem","attrs":{"checked":true},"content":[{"type":"paragraph","content":[{"type":"text","text":"Chleb"}]}]}]}]}""";

        private static HttpClient CreateClient(CustomWebApplicationFactory factory) =>
            factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        private static async Task<string> RegisterAndLoginAsync(HttpClient client)
        {
            var username = $"u{Guid.NewGuid():N}"[..16];
            var email = $"{username}@test.local";
            await client.PostAsJsonAsync("/api/auth/register",
                new { username, email, password = "Password123!", turnstileToken = TestTurnstileToken, consentAccepted = true });

            var link = TestEmailCapture.VerificationLinks[email];
            var token = link[(link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..];
            await client.GetAsync($"/api/auth/verify-email?token={token}");

            var login = await client.PostAsJsonAsync("/api/auth/login",
                new { username, password = "Password123!", turnstileToken = TestTurnstileToken, consentAccepted = true });
            login.EnsureSuccessStatusCode();
            return (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        }

        // Zwraca klucz urzadzenia sparowanego z kontem wlasciciela JWT.
        private static async Task<string> PairDeviceAsync(HttpClient client, string jwt)
        {
            var init = new HttpRequestMessage(HttpMethod.Post, "/api/devices/pair-init");
            init.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            var initResponse = await client.SendAsync(init);
            initResponse.EnsureSuccessStatusCode();
            var code = (await initResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

            var claim = await client.PostAsJsonAsync("/api/devices/claim", new { code, deviceName = "test-esp32" });
            claim.EnsureSuccessStatusCode();
            return (await claim.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!;
        }

        private static async Task<Guid> CreateNoteAsync(HttpClient client, string jwt, string? textContent)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/notes")
            {
                Content = JsonContent.Create(new { title = "Zakupy", textContent, color = (string?)null })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        private static Task<HttpResponseMessage> PutTaskAsync(HttpClient client, string? apiKey, Guid noteId, string index, string rawBody)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/api/device-notes/{noteId}/tasks/{index}")
            {
                Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
            };
            if (apiKey is not null) request.Headers.Add("X-Api-Key", apiKey);
            return client.SendAsync(request);
        }

        private static async Task<List<JsonElement>> GetTaskBlocksAsync(HttpClient client, string apiKey, Guid noteId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/device-notes/{noteId}");
            request.Headers.Add("X-Api-Key", apiKey);
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("textBlocks").EnumerateArray()
                .Where(b => b.GetProperty("type").GetString() == "taskItem").ToList();
        }

        [Fact]
        public async Task SetTaskChecked_SetsStateAndReturnsItUnderChecked()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var jwt = await RegisterAndLoginAsync(client);
            var apiKey = await PairDeviceAsync(client, jwt);
            var noteId = await CreateNoteAsync(client, jwt, TasksDoc);

            var response = await PutTaskAsync(client, apiKey, noteId, "0", """{"checked":true}""");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("checked").GetBoolean());

            // GET urzadzenia widzi nowy stan, a taskIndex jest w odpowiedzi
            var tasks = await GetTaskBlocksAsync(client, apiKey, noteId);
            Assert.True(tasks[0].GetProperty("checked").GetBoolean());
            Assert.Equal(0, tasks[0].GetProperty("taskIndex").GetInt32());
            Assert.Equal(1, tasks[1].GetProperty("taskIndex").GetInt32());

            // idempotencja: to samo zadanie ponownie -> 200 i ten sam stan
            var again = await PutTaskAsync(client, apiKey, noteId, "0", """{"checked":true}""");
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
            Assert.True((await GetTaskBlocksAsync(client, apiKey, noteId))[0].GetProperty("checked").GetBoolean());
        }

        [Fact]
        public async Task SetTaskChecked_WebAppSeesNewStateInTextContent()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var jwt = await RegisterAndLoginAsync(client);
            var apiKey = await PairDeviceAsync(client, jwt);
            var noteId = await CreateNoteAsync(client, jwt, TasksDoc);

            await PutTaskAsync(client, apiKey, noteId, "1", """{"checked":false}""");

            var get = new HttpRequestMessage(HttpMethod.Get, $"/api/notes/{noteId}");
            get.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            var note = await (await client.SendAsync(get)).Content.ReadFromJsonAsync<JsonElement>();
            var doc = JsonDocument.Parse(note.GetProperty("textContent").GetString()!);
            var items = doc.RootElement.GetProperty("content")[0].GetProperty("content");
            Assert.False(items[0].GetProperty("attrs").GetProperty("checked").GetBoolean());
            Assert.False(items[1].GetProperty("attrs").GetProperty("checked").GetBoolean());
        }

        [Fact]
        public async Task SetTaskChecked_WithoutApiKey_ReturnsUnauthorized()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);

            var response = await PutTaskAsync(client, null, Guid.NewGuid(), "0", """{"checked":true}""");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task SetTaskChecked_OtherUsersNote_ReturnsNotFound()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var ownerJwt = await RegisterAndLoginAsync(client);
            var noteId = await CreateNoteAsync(client, ownerJwt, TasksDoc);
            var attackerJwt = await RegisterAndLoginAsync(client);
            var attackerKey = await PairDeviceAsync(client, attackerJwt);

            var response = await PutTaskAsync(client, attackerKey, noteId, "0", """{"checked":true}""");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData("5", """{"checked":true}""")]          // indeks poza zakresem
        [InlineData("-1", """{"checked":true}""")]         // ujemny indeks
        [InlineData("0", """{}""")]                        // brak pola checked
        [InlineData("0", """{"checked":"tak"}""")]         // checked nie jest bool
        [InlineData("0", """{"checked":null}""")]          // checked null
        public async Task SetTaskChecked_BadIndexOrBody_ReturnsBadRequest(string index, string body)
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var jwt = await RegisterAndLoginAsync(client);
            var apiKey = await PairDeviceAsync(client, jwt);
            var noteId = await CreateNoteAsync(client, jwt, TasksDoc);

            var response = await PutTaskAsync(client, apiKey, noteId, index, body);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task SetTaskChecked_LegacyPlainTextNote_ReturnsBadRequest()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var jwt = await RegisterAndLoginAsync(client);
            var apiKey = await PairDeviceAsync(client, jwt);
            var noteId = await CreateNoteAsync(client, jwt, "stara zwykla tresc");

            var response = await PutTaskAsync(client, apiKey, noteId, "0", """{"checked":true}""");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---- POST /api/device-notes/{id}/drawings (nowy rysunek z urzadzenia) ----

        private const string ValidStrokes =
            """{"strokes":[{"color":"#2d2640","width":3,"points":[{"x":10,"y":12},{"x":40,"y":55}]}],"canvasWidth":320,"canvasHeight":190}""";

        private static Task<HttpResponseMessage> PostDrawingAsync(HttpClient client, string? apiKey, Guid noteId, string? strokesJson)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/device-notes/{noteId}/drawings")
            {
                Content = JsonContent.Create(new { strokesJson })
            };
            if (apiKey is not null) request.Headers.Add("X-Api-Key", apiKey);
            return client.SendAsync(request);
        }

        [Fact]
        public async Task AddDrawing_WithoutApiKey_ReturnsUnauthorized()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);

            var response = await PostDrawingAsync(client, null, Guid.NewGuid(), ValidStrokes);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AddDrawing_OtherUsersOrUnknownNote_ReturnsNotFound()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var ownerJwt = await RegisterAndLoginAsync(client);
            var noteId = await CreateNoteAsync(client, ownerJwt, TasksDoc);
            var attackerKey = await PairDeviceAsync(client, await RegisterAndLoginAsync(client));

            Assert.Equal(HttpStatusCode.NotFound, (await PostDrawingAsync(client, attackerKey, noteId, ValidStrokes)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await PostDrawingAsync(client, attackerKey, Guid.NewGuid(), ValidStrokes)).StatusCode);
        }

        [Theory]
        [InlineData("to nie json")]
        [InlineData("""{"strokes":[]}""")]
        [InlineData("""{"strokes":[{"color":"#000000","width":3,"points":[{"x":1,"y":1}]}]}""")]
        [InlineData("""{"strokes":[{"color":"zielony","width":3,"points":[{"x":1,"y":1},{"x":2,"y":2}]}]}""")]
        [InlineData(null)]
        public async Task AddDrawing_InvalidStrokes_ReturnsBadRequest(string? strokes)
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var jwt = await RegisterAndLoginAsync(client);
            var apiKey = await PairDeviceAsync(client, jwt);
            var noteId = await CreateNoteAsync(client, jwt, TasksDoc);

            var response = await PostDrawingAsync(client, apiKey, noteId, strokes);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task AddDrawing_EmptyBody_ReturnsBadRequest()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var jwt = await RegisterAndLoginAsync(client);
            var apiKey = await PairDeviceAsync(client, jwt);
            var noteId = await CreateNoteAsync(client, jwt, TasksDoc);
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/device-notes/{noteId}/drawings")
            {
                Content = new StringContent("", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Api-Key", apiKey);

            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
        }

        [Fact]
        public async Task AddDrawing_Success_ReturnsCreatedAndDeviceGetListsNewDrawing()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var jwt = await RegisterAndLoginAsync(client);
            var apiKey = await PairDeviceAsync(client, jwt);
            var noteId = await CreateNoteAsync(client, jwt, TasksDoc);

            var response = await PostDrawingAsync(client, apiKey, noteId, ValidStrokes);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            var drawingId = created.GetProperty("id").GetGuid();
            Assert.Equal(ValidStrokes, created.GetProperty("strokesJson").GetString());
            Assert.Equal(0, created.GetProperty("sortOrder").GetInt32());

            var get = new HttpRequestMessage(HttpMethod.Get, $"/api/device-notes/{noteId}");
            get.Headers.Add("X-Api-Key", apiKey);
            var note = await (await client.SendAsync(get)).Content.ReadFromJsonAsync<JsonElement>();
            var drawings = note.GetProperty("drawings");
            Assert.Equal(1, drawings.GetArrayLength());
            Assert.Equal(drawingId, drawings[0].GetProperty("id").GetGuid());
            Assert.Equal(ValidStrokes, drawings[0].GetProperty("strokesJson").GetString());
            // tekst notatki i taski nadal widoczne dla urzadzenia
            Assert.Equal(2, note.GetProperty("textBlocks").EnumerateArray().Count(b => b.GetProperty("type").GetString() == "taskItem"));
        }

        [Fact]
        public async Task AddDrawing_WebAppSeesDrawingBlockInTextContent()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var jwt = await RegisterAndLoginAsync(client);
            var apiKey = await PairDeviceAsync(client, jwt);
            var noteId = await CreateNoteAsync(client, jwt, null);

            var response = await PostDrawingAsync(client, apiKey, noteId, ValidStrokes);
            var drawingId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

            var get = new HttpRequestMessage(HttpMethod.Get, $"/api/notes/{noteId}");
            get.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            var note = await (await client.SendAsync(get)).Content.ReadFromJsonAsync<JsonElement>();
            var doc = JsonDocument.Parse(note.GetProperty("textContent").GetString()!);
            var block = doc.RootElement.GetProperty("content")[0];
            Assert.Equal("drawingBlock", block.GetProperty("type").GetString());
            Assert.Equal(drawingId, block.GetProperty("attrs").GetProperty("drawingId").GetString());
            Assert.Equal(drawingId, note.GetProperty("drawings")[0].GetProperty("id").GetString());
        }
    }
}
