using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NoteEz_Server.Data;
using NoteEz_Server.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NoteEz_Server.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
                return Conflict("Użytkownik już istnieje.");

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == dto.Username);
            if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized();

            var accessToken = GenerateAccessToken(user);
            var refreshToken = await GenerateAndStoreRefreshToken(user.Id);

            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            Console.WriteLine($"User {user.Username} logged in. Access token: {accessToken}, Refresh token: {refreshToken}");
            Console.WriteLine($"Claims: {string.Join(", ", User.Claims.Select(c => $"{c.Type}: {c.Value}"))}");

            return Ok(new { accessToken });
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
