using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace NoteEz_Server.Tests.Integration
{
    // Testy koncowo-do-konca dla auth + izolacji notatek miedzy uzytkownikami.
    // Kazdy test tworzy WLASNA fabryke (wlasna InMemory baza + wlasny rate limiter),
    // zeby testy nie zalewaly sobie nawzajem tego samego okna limitu zapytan na /api/auth
    // (limiter partycjonuje po adresie IP, ktory w TestServer jest identyczny dla wszystkich
    // wywolan w ramach jednego procesu - wspoldzielona fabryka = wspoldzielony licznik 429).
    public class AuthControllerTests
    {
        // Username ma limit 32 znakow (RegisterDto/LoginDto) - krotszy prefiks + krotszy
        // wycinek GUID-a, zeby zmiescic sie w limicie a nadal byc unikalnym miedzy testami.
        private static string NewUsername() => $"u{Guid.NewGuid():N}"[..16];

        private static string? ExtractCookie(HttpResponseMessage response, string cookieName)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var cookies)) return null;
            foreach (var cookie in cookies)
            {
                if (cookie.StartsWith(cookieName + "="))
                {
                    var end = cookie.IndexOf(';');
                    return end >= 0 ? cookie[..end] : cookie;
                }
            }
            return null;
        }

        private static async Task<string> RegisterAndLoginAsync(HttpClient client, string username, string password = "Password123!")
        {
            await client.PostAsJsonAsync("/api/auth/register", new { username, password });
            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
            loginResponse.EnsureSuccessStatusCode();
            var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("accessToken").GetString()!;
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsUnauthorized()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var username = NewUsername();
            await client.PostAsJsonAsync("/api/auth/register", new { username, password = "CorrectPass1" });

            var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "WrongPass1" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_UnknownUser_ReturnsUnauthorized()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { username = NewUsername(), password = "Whatever123" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Register_TooShortPassword_ReturnsBadRequest()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync(
                "/api/auth/register", new { username = NewUsername(), password = "short" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_InvalidUsernameCharacters_ReturnsBadRequest()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync(
                "/api/auth/register", new { username = "bad name!!", password = "GoodPassword1" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_DuplicateUsername_ReturnsConflict()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var username = NewUsername();
            var first = await client.PostAsJsonAsync("/api/auth/register", new { username, password = "Password123!" });
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var response = await client.PostAsJsonAsync("/api/auth/register", new { username, password = "Password123!" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Refresh_RotatesTokenAndInvalidatesPrevious()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var username = NewUsername();
            await client.PostAsJsonAsync("/api/auth/register", new { username, password = "Password123!" });
            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "Password123!" });
            var firstCookie = ExtractCookie(loginResponse, "refreshToken");
            Assert.NotNull(firstCookie);

            var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            refreshRequest.Headers.Add("Cookie", firstCookie!);
            var refreshResponse = await client.SendAsync(refreshRequest);
            Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

            var secondCookie = ExtractCookie(refreshResponse, "refreshToken");
            Assert.NotNull(secondCookie);
            Assert.NotEqual(firstCookie, secondCookie);

            // stary cookie zostal juz zrotowany/uniewazniony - ponowne uzycie musi sie nie udac
            var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            replayRequest.Headers.Add("Cookie", firstCookie!);
            var replayResponse = await client.SendAsync(replayRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
        }

        [Fact]
        public async Task Refresh_ReuseOfRevokedToken_RevokesAllSessionsForUser()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var username = NewUsername();
            await client.PostAsJsonAsync("/api/auth/register", new { username, password = "Password123!" });
            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "Password123!" });
            var firstCookie = ExtractCookie(loginResponse, "refreshToken");
            Assert.NotNull(firstCookie);

            // rotacja raz (pierwszy -> drugi token)
            var refreshRequest1 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            refreshRequest1.Headers.Add("Cookie", firstCookie!);
            var refreshResponse1 = await client.SendAsync(refreshRequest1);
            var secondCookie = ExtractCookie(refreshResponse1, "refreshToken");
            Assert.NotNull(secondCookie);

            // odtworzenie juz uniewaznionego pierwszego cookie - powinno wywolac wykrycie kradziezy
            var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            replayRequest.Headers.Add("Cookie", firstCookie!);
            var replayResponse = await client.SendAsync(replayRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

            // token drugiej generacji, mimo ze byl wazny, musi zostac teraz tez uniewazniony
            var refreshRequest2 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
            refreshRequest2.Headers.Add("Cookie", secondCookie!);
            var refreshResponse2 = await client.SendAsync(refreshRequest2);
            Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse2.StatusCode);
        }

        [Fact]
        public async Task Refresh_NoCookie_ReturnsUnauthorized()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var response = await client.PostAsync("/api/auth/refresh", null);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateNote_WithoutToken_ReturnsUnauthorized()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync(
                "/api/notes", new { title = "test", textContent = (string?)null, color = (string?)null });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateNote_EmptyTitle_ReturnsBadRequest()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var token = await RegisterAndLoginAsync(client, NewUsername());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsJsonAsync(
                "/api/notes", new { title = "", textContent = (string?)null, color = (string?)null });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateNote_InvalidColor_ReturnsBadRequest()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var token = await RegisterAndLoginAsync(client, NewUsername());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsJsonAsync(
                "/api/notes", new { title = "Tytul", textContent = (string?)null, color = "not-a-color" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateNote_ValidRequest_ReturnsCreated()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var token = await RegisterAndLoginAsync(client, NewUsername());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsJsonAsync(
                "/api/notes", new { title = "Moja notatka", textContent = (string?)null, color = "#8963ba" });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task Notes_AreIsolatedBetweenUsers()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var tokenA = await RegisterAndLoginAsync(client, NewUsername());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
            var createResponse = await client.PostAsJsonAsync(
                "/api/notes", new { title = "Sekret A", textContent = (string?)null, color = (string?)null });
            var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
            var noteId = created.GetProperty("id").GetGuid();

            var tokenB = await RegisterAndLoginAsync(client, NewUsername());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

            var getResponse = await client.GetAsync($"/api/notes/{noteId}");

            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task LoginRateLimiter_BlocksAfterThreshold()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = factory.CreateClient();
            var username = NewUsername();

            HttpStatusCode? lastStatus = null;
            for (var i = 0; i < 15; i++)
            {
                var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "whatever-wrong" });
                lastStatus = response.StatusCode;
                if (lastStatus == HttpStatusCode.TooManyRequests) break;
            }

            Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
        }
    }
}
