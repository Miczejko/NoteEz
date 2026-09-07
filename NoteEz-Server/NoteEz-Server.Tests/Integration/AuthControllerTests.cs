using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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

        private static string EmailFor(string username) => $"{username}@test.local";

        // FakeTurnstileService akceptuje kazdy niepusty token - w testach nie ma
        // prawdziwego widgetu CAPTCHA, wiec wysylamy stala wartosc.
        private const string TestTurnstileToken = "test-turnstile-token";

        // verify-email robi Redirect() na koniec (do frontendu) - nie chcemy, zeby test-owy
        // HttpClient probowal go realnie nastepnie "kliknac" (skonczylby sie 404, bo tu nie ma
        // frontendu), wiec wylaczamy automatyczne podazanie za przekierowaniami.
        private static HttpClient CreateClient(CustomWebApplicationFactory factory) =>
            factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

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

        // Rejestracja jest dwuetapowa - konto powstaje dopiero po "kliknieciu" linku
        // weryfikacyjnego. W testach prawdziwy mail jest podmieniony na FakeEmailService,
        // ktory zapisuje link do TestEmailCapture - tutaj go odczytujemy i wywolujemy,
        // zeby dokonczyc zakladanie konta tak jak zrobilby to uzytkownik klikajac w mailu.
        private static async Task VerifyEmailAsync(HttpClient client, string email)
        {
            Assert.True(
                TestEmailCapture.VerificationLinks.TryGetValue(email, out var link),
                $"Nie wyslano linku weryfikacyjnego na adres {email}.");

            var tokenIndex = link!.IndexOf("token=", StringComparison.Ordinal);
            var token = link[(tokenIndex + "token=".Length)..];

            var response = await client.GetAsync($"/api/auth/verify-email?token={token}");
            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
            var location = response.Headers.Location?.ToString() ?? "";
            Assert.Contains("verified=1", location);
        }

        private static async Task<string> RegisterAndLoginAsync(HttpClient client, string username, string password = "Password123!")
        {
            var email = EmailFor(username);
            await client.PostAsJsonAsync(
                "/api/auth/register", new { username, email, password, turnstileToken = TestTurnstileToken, consentAccepted = true });
            await VerifyEmailAsync(client, email);

            var loginResponse = await client.PostAsJsonAsync(
                "/api/auth/login", new { username, password, turnstileToken = TestTurnstileToken, consentAccepted = true });
            loginResponse.EnsureSuccessStatusCode();
            var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("accessToken").GetString()!;
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsUnauthorized()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var username = NewUsername();
            var email = EmailFor(username);
            await client.PostAsJsonAsync(
                "/api/auth/register",
                new { username, email, password = "CorrectPass1", turnstileToken = TestTurnstileToken, consentAccepted = true });
            await VerifyEmailAsync(client, email);

            var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { username, password = "WrongPass1", turnstileToken = TestTurnstileToken, consentAccepted = true });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_UnknownUser_ReturnsUnauthorized()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { username = NewUsername(), password = "Whatever123", turnstileToken = TestTurnstileToken, consentAccepted = true });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_UnverifiedRegistration_ReturnsUnauthorized()
        {
            // Rejestracja bez kliknietego linku weryfikacyjnego nie powinna pozwalac
            // na zalogowanie - konto jeszcze nie istnieje, jest tylko PendingRegistration.
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var username = NewUsername();
            var registerResponse = await client.PostAsJsonAsync(
                "/api/auth/register",
                new { username, email = EmailFor(username), password = "Password123!", turnstileToken = TestTurnstileToken, consentAccepted = true });
            Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

            var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { username, password = "Password123!", turnstileToken = TestTurnstileToken, consentAccepted = true });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Register_TooShortPassword_ReturnsBadRequest()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var username = NewUsername();
            var response = await client.PostAsJsonAsync(
                "/api/auth/register",
                new { username, email = EmailFor(username), password = "short", turnstileToken = TestTurnstileToken, consentAccepted = true });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_InvalidUsernameCharacters_ReturnsBadRequest()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var response = await client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    username = "bad name!!",
                    email = "bad-username@test.local",
                    password = "GoodPassword1",
                    turnstileToken = TestTurnstileToken,
                    consentAccepted = true,
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_InvalidEmail_ReturnsBadRequest()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var response = await client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    username = NewUsername(),
                    email = "not-an-email",
                    password = "GoodPassword1",
                    turnstileToken = TestTurnstileToken,
                    consentAccepted = true,
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_DuplicateUsername_ReturnsConflict()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var username = NewUsername();
            var email = EmailFor(username);
            var first = await client.PostAsJsonAsync(
                "/api/auth/register",
                new { username, email, password = "Password123!", turnstileToken = TestTurnstileToken, consentAccepted = true });
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            await VerifyEmailAsync(client, email);

            // ta sama nazwa uzytkownika, ale inny e-mail - konflikt powinien nadal wystapic,
            // bo konto z tym username juz istnieje (zweryfikowane powyzej)
            var response = await client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    username,
                    email = EmailFor(NewUsername()),
                    password = "Password123!",
                    turnstileToken = TestTurnstileToken,
                    consentAccepted = true,
                });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsOkWithoutRevealingConflict()
        {
            // Security: odpowiedz na rejestracje z juz zajetym e-mailem NIE moze sie roznic
            // od udanej rejestracji - inaczej API zdradzaloby, ktore adresy sa zarejestrowane
            // (enumeration). Zamiast bledu wysylamy powiadomienie na ten adres i zwracamy 200.
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var firstUsername = NewUsername();
            var email = EmailFor(firstUsername);
            var first = await client.PostAsJsonAsync(
                "/api/auth/register",
                new { username = firstUsername, email, password = "Password123!", turnstileToken = TestTurnstileToken, consentAccepted = true });
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            await VerifyEmailAsync(client, email);

            var response = await client.PostAsJsonAsync(
                "/api/auth/register",
                new { username = NewUsername(), email, password = "Password123!", turnstileToken = TestTurnstileToken, consentAccepted = true });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(email, TestEmailCapture.RegistrationAttemptNotices);
        }

        [Fact]
        public async Task Refresh_RotatesTokenAndInvalidatesPrevious()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var username = NewUsername();
            var email = EmailFor(username);
            await client.PostAsJsonAsync(
                "/api/auth/register",
                new { username, email, password = "Password123!", turnstileToken = TestTurnstileToken, consentAccepted = true });
            await VerifyEmailAsync(client, email);
            var loginResponse = await client.PostAsJsonAsync(
                "/api/auth/login", new { username, password = "Password123!", turnstileToken = TestTurnstileToken, consentAccepted = true });
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
            var client = CreateClient(factory);
            var username = NewUsername();
            var email = EmailFor(username);
            await client.PostAsJsonAsync(
                "/api/auth/register",
                new { username, email, password = "Password123!", turnstileToken = TestTurnstileToken, consentAccepted = true });
            await VerifyEmailAsync(client, email);
            var loginResponse = await client.PostAsJsonAsync(
                "/api/auth/login", new { username, password = "Password123!", turnstileToken = TestTurnstileToken, consentAccepted = true });
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
            var client = CreateClient(factory);
            var response = await client.PostAsync("/api/auth/refresh", null);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ForgotPassword_UnknownEmail_ReturnsOkWithoutRevealingConflict()
        {
            // Security: nie zdradzamy, czy dany e-mail istnieje w bazie - odpowiedz jest
            // taka sama niezaleznie od tego, czy konto istnieje.
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var response = await client.PostAsJsonAsync(
                "/api/auth/password/forgot",
                new { email = "nieistniejacy@test.local", turnstileToken = TestTurnstileToken, consentAccepted = true });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ResetPassword_ValidToken_AllowsLoginWithNewPassword()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var username = NewUsername();
            var email = EmailFor(username);
            await client.PostAsJsonAsync(
                "/api/auth/register",
                new { username, email, password = "OldPassword1", turnstileToken = TestTurnstileToken, consentAccepted = true });
            await VerifyEmailAsync(client, email);

            var forgotResponse = await client.PostAsJsonAsync(
                "/api/auth/password/forgot", new { email, turnstileToken = TestTurnstileToken, consentAccepted = true });
            Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);

            Assert.True(TestEmailCapture.PasswordResetLinks.TryGetValue(email, out var resetLink));
            var tokenIndex = resetLink!.IndexOf("token=", StringComparison.Ordinal);
            var token = resetLink[(tokenIndex + "token=".Length)..];

            var resetResponse = await client.PostAsJsonAsync(
                "/api/auth/password/reset", new { token, newPassword = "NewPassword1" });
            Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

            var oldLoginResponse = await client.PostAsJsonAsync(
                "/api/auth/login", new { username, password = "OldPassword1", turnstileToken = TestTurnstileToken, consentAccepted = true });
            Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);

            var newLoginResponse = await client.PostAsJsonAsync(
                "/api/auth/login", new { username, password = "NewPassword1", turnstileToken = TestTurnstileToken, consentAccepted = true });
            Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);
        }

        [Fact]
        public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);

            var response = await client.PostAsJsonAsync(
                "/api/auth/password/reset", new { token = "not-a-real-token", newPassword = "NewPassword1" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateNote_WithoutToken_ReturnsUnauthorized()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
            var response = await client.PostAsJsonAsync(
                "/api/notes", new { title = "test", textContent = (string?)null, color = (string?)null });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateNote_EmptyTitle_ReturnsBadRequest()
        {
            using var factory = new CustomWebApplicationFactory();
            var client = CreateClient(factory);
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
            var client = CreateClient(factory);
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
            var client = CreateClient(factory);
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
            var client = CreateClient(factory);
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
            var client = CreateClient(factory);
            var username = NewUsername();

            HttpStatusCode? lastStatus = null;
            for (var i = 0; i < 15; i++)
            {
                var response = await client.PostAsJsonAsync(
                    "/api/auth/login", new { username, password = "whatever-wrong", turnstileToken = TestTurnstileToken, consentAccepted = true });
                lastStatus = response.StatusCode;
                if (lastStatus == HttpStatusCode.TooManyRequests) break;
            }

            Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
        }
    }
}
