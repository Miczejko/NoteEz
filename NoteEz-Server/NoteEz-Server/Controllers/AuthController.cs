using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NoteEz_Server.Data;
using NoteEz_Server.Models;
using NoteEz_Server.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NoteEz_Server.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;
        private readonly TurnstileService _turnstileService;

        public AuthController(AppDbContext db, IConfiguration config, EmailService emailService, TurnstileService turnstileService)
        {
            _db = db;
            _config = config;
            _emailService = emailService;
            _turnstileService = turnstileService;
        }

        private async Task<bool> VerifyCaptchaAsync(string token)
        {
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            return await _turnstileService.VerifyAsync(token, remoteIp);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            if (!await VerifyCaptchaAsync(dto.TurnstileToken))
                return BadRequest("Weryfikacja CAPTCHA nie powiodła się. Spróbuj ponownie.");

            if (!dto.ConsentAccepted)
                return BadRequest("Musisz zapoznać się z polityką prywatności i regulaminem.");

            if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
                return Conflict("Użytkownik już istnieje.");

            // Nie zdradzamy w odpowiedzi, czy podany e-mail jest juz zajety (enumeration) -
            // zamiast bledu wysylamy na ten adres informacje o probie rejestracji i konczymy
            // tak samo, jak przy udanej rejestracji.
            if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            {
                await _emailService.SendRegistrationAttemptOnExistingAccountEmailAsync(dto.Email);
                return Ok();
            }

            // Konto jeszcze nie istnieje - dane czekaja na potwierdzenie e-maila.
            // Usuwamy ewentualne wczesniejsze, niedokonczone proby rejestracji tej samej
            // nazwy/e-maila, zeby uzytkownik mogl po prostu sprobowac ponownie.
            var existingPending = await _db.PendingRegistrations
                .Where(p => p.Username == dto.Username || p.Email == dto.Email)
                .ToListAsync();
            _db.PendingRegistrations.RemoveRange(existingPending);

            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            var tokenHash = Sha256(rawToken);

            _db.PendingRegistrations.Add(new PendingRegistration
            {
                Id = Guid.NewGuid(),
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                ConsentAcceptedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // Link budujemy z zaufanej, skonfigurowanej wartosci, a nie z naglowka Host -
            // Host jest kontrolowany przez wywolujacego i mozna by nim podmienic link
            // wysylany w mailu (host header injection / phishing z zaufanego nadawcy).
            var backendBaseUrl = _config["App:BackendBaseUrl"]?.TrimEnd('/') ?? "";
            var verificationLink = $"{backendBaseUrl}/api/auth/verify-email?token={rawToken}";
            await _emailService.SendVerificationEmailAsync(dto.Email, verificationLink);

            return Ok();
        }

        [HttpGet("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            var frontendBaseUrl = _config["App:FrontendBaseUrl"]?.TrimEnd('/') ?? "";

            if (string.IsNullOrEmpty(token))
                return Redirect($"{frontendBaseUrl}/login?verified=0");

            var tokenHash = Sha256(token);
            var pending = await _db.PendingRegistrations.FirstOrDefaultAsync(p => p.TokenHash == tokenHash);

            if (pending is null || pending.ExpiresAt <= DateTime.UtcNow)
                return Redirect($"{frontendBaseUrl}/login?verified=0");

            // ktos mogl w miedzyczasie zajac nazwe/e-mail przy innej rejestracji
            if (await _db.Users.AnyAsync(u => u.Username == pending.Username || u.Email == pending.Email))
            {
                _db.PendingRegistrations.Remove(pending);
                await _db.SaveChangesAsync();
                return Redirect($"{frontendBaseUrl}/login?verified=0");
            }

            _db.Users.Add(new User
            {
                Username = pending.Username,
                Email = pending.Email,
                PasswordHash = pending.PasswordHash,
                ConsentAcceptedAt = pending.ConsentAcceptedAt
            });
            _db.PendingRegistrations.Remove(pending);
            await _db.SaveChangesAsync();

            return Redirect($"{frontendBaseUrl}/login?verified=1");
        }

        [HttpPost("password/forgot")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            if (!await VerifyCaptchaAsync(dto.TurnstileToken))
                return BadRequest("Weryfikacja CAPTCHA nie powiodła się. Spróbuj ponownie.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            // Zawsze zwracamy 200 niezaleznie od tego, czy e-mail istnieje w bazie -
            // inaczej formularz zdradzalby, ktore adresy sa zarejestrowane (enumeration).
            if (user != null)
                await IssuePasswordResetTokenAsync(user);

            return Ok();
        }

        [HttpPost("password/change-request")]
        [Authorize]
        public async Task<IActionResult> RequestPasswordChange()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return Unauthorized();

            await IssuePasswordResetTokenAsync(user);
            return Ok();
        }

        [HttpPost("password/reset")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            var tokenHash = Sha256(dto.Token);
            var stored = await _db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (stored is null || stored.ExpiresAt <= DateTime.UtcNow)
                return BadRequest("Link do zmiany hasła jest nieprawidłowy lub wygasł.");

            var user = await _db.Users.FindAsync(stored.UserId);
            if (user is null)
            {
                _db.PasswordResetTokens.Remove(stored);
                await _db.SaveChangesAsync();
                return BadRequest("Link do zmiany hasła jest nieprawidłowy lub wygasł.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            _db.PasswordResetTokens.Remove(stored);

            // zmiana hasla uniewaznia wszystkie aktywne sesje - wymuszamy ponowne logowanie wszedzie
            var activeSessions = await _db.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedAt == null)
                .ToListAsync();
            foreach (var s in activeSessions) s.RevokedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok();
        }

        private async Task IssuePasswordResetTokenAsync(User user)
        {
            var existingTokens = await _db.PasswordResetTokens
                .Where(t => t.UserId == user.Id)
                .ToListAsync();
            _db.PasswordResetTokens.RemoveRange(existingTokens);

            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            _db.PasswordResetTokens.Add(new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = Sha256(rawToken),
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            });
            await _db.SaveChangesAsync();

            var frontendBaseUrl = _config["App:FrontendBaseUrl"]?.TrimEnd('/') ?? "";
            var resetLink = $"{frontendBaseUrl}/reset-password?token={rawToken}";
            await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);
        }

        // hash "placebo" uzywany, gdy uzytkownik nie istnieje - BCrypt.Verify zajmuje wtedy
        // tyle samo czasu co dla istniejacego konta, zeby czas odpowiedzi nie zdradzal,
        // czy dana nazwa uzytkownika jest zarejestrowana (username enumeration przez timing)
        private const string DummyPasswordHash = "$2a$11$CwTycUXWue0Thq9StjUM0uJ8lqM8L.ku1XdCz4CQ9Mzp2j5c2K5C.";

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            if (!await VerifyCaptchaAsync(dto.TurnstileToken))
                return BadRequest("Weryfikacja CAPTCHA nie powiodła się. Spróbuj ponownie.");

            var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == dto.Username);
            var passwordOk = BCrypt.Net.BCrypt.Verify(dto.Password, user?.PasswordHash ?? DummyPasswordHash);

            if (user is null || !passwordOk)
                return Unauthorized();

            var accessToken = GenerateAccessToken(user);
            var refreshToken = await GenerateAndStoreRefreshToken(user.Id);

            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                // The frontend's Cloudflare Worker (src/worker.js) reverse-proxies /api/*
                // to this Azure backend server-side, so the browser only ever talks to
                // noteez.online - this cookie is genuinely first-party/same-site from its
                // point of view. (SameSite=None was tried first since Azure's free tier
                // has no custom-domain option, but modern browsers block third-party
                // cookies outright regardless of SameSite, so that alone didn't work.)
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            return Ok(new { accessToken });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var rawToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(rawToken))
                return Unauthorized();

            var hash = Sha256(rawToken);
            var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (stored is null || stored.ExpiresAt <= DateTime.UtcNow)
            {
                Response.Cookies.Delete("refreshToken");
                return Unauthorized();
            }

            if (stored.RevokedAt != null)
            {
                // token juz raz zuzyty/uniewazniony wraca ponownie - to sygnal kradziezy
                // (np. skopiowany zanim wlasciciel go zuzyl). Uniewazniamy WSZYSTKIE sesje
                // tego uzytkownika, zeby wymusic ponowne logowanie wszedzie.
                var allActive = await _db.RefreshTokens
                    .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                    .ToListAsync();
                foreach (var t in allActive) t.RevokedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                Response.Cookies.Delete("refreshToken");
                return Unauthorized();
            }

            var user = await _db.Users.FindAsync(stored.UserId);
            if (user is null)
            {
                Response.Cookies.Delete("refreshToken");
                return Unauthorized();
            }

            // rotacja: stary refresh token natychmiast przestaje dzialac, wydajemy nowy
            var newRefreshToken = await GenerateAndStoreRefreshToken(user.Id);
            stored.RevokedAt = DateTime.UtcNow;
            stored.ReplacedByTokenHash = Sha256(newRefreshToken);
            await _db.SaveChangesAsync();

            Response.Cookies.Append("refreshToken", newRefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                // The frontend's Cloudflare Worker (src/worker.js) reverse-proxies /api/*
                // to this Azure backend server-side, so the browser only ever talks to
                // noteez.online - this cookie is genuinely first-party/same-site from its
                // point of view. (SameSite=None was tried first since Azure's free tier
                // has no custom-domain option, but modern browsers block third-party
                // cookies outright regardless of SameSite, so that alone didn't work.)
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            var accessToken = GenerateAccessToken(user);
            return Ok(new { accessToken, username = user.Username });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var rawToken = Request.Cookies["refreshToken"];
            if (rawToken != null)
            {
                var hash = Sha256(rawToken);
                var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
                if (stored != null) stored.RevokedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            Response.Cookies.Delete("refreshToken");
            return Ok();
        }

        [HttpGet("sessions")]
        [Authorize]
        public async Task<IActionResult> GetSessions()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var sessions = await _db.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
                .Select(t => new { t.Id, t.ExpiresAt })
                .ToListAsync();
            return Ok(sessions);
        }

        [HttpPost("sessions/{id}/revoke")]
        [Authorize]
        public async Task<IActionResult> RevokeSession(Guid id)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var token = await _db.RefreshTokens
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (token is null) return NotFound();
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok();
        }

        private string GenerateAccessToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        private async Task<string> GenerateAndStoreRefreshToken(Guid userId)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var hash = Sha256(rawToken);

            _db.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                TokenHash = hash,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RevokedAt = null
            });
            await _db.SaveChangesAsync();

            return rawToken; // surowy token idzie do cookie, hash do bazy
        }

        private static string Sha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
    }
}
