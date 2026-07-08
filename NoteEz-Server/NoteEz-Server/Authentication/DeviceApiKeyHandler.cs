using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NoteEz_Server.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;


namespace NoteEz_Server.Authentication
{

    public class DeviceApiKeyHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly AppDbContext _db;

        public DeviceApiKeyHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, AppDbContext db)
            : base(options, logger, encoder)
        {
            _db = db;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
                return AuthenticateResult.Fail("Brak nagłówka X-Api-Key.");

            var apiKey = apiKeyHeader.ToString();
            var hash = Sha256(apiKey);

            var device = await _db.Devices.FirstOrDefaultAsync(d => d.ApiKeyHash == hash);
            if (device is null)
                return AuthenticateResult.Fail("Nieprawidłowy klucz API.");

            device.LastSeenAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, device.UserId.ToString()),
            new Claim("DeviceId", device.Id.ToString())
        };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }

        private static string Sha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
    }
}
