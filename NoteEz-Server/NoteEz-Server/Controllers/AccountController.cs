using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;
using NoteEz_Server.Models;
using NoteEz_Server.Services;
using System.Security.Claims;

namespace NoteEz_Server.Controllers
{
    // Realizuje prawo do usuniecia danych ("prawo do bycia zapomnianym") zadeklarowane
    // w polityce prywatnosci - kasuje konto wraz ze wszystkimi powiazanymi danymi.
    [ApiController]
    [Route("api/account")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly NoteAudioService _audioService;

        public AccountController(AppDbContext db, NoteAudioService audioService)
        {
            _db = db;
            _audioService = audioService;
        }

        private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpDelete]
        public async Task<IActionResult> DeleteAccount(AccountDeleteDto dto)
        {
            var user = await _db.Users.FindAsync(UserId);
            if (user is null) return Unauthorized();

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return BadRequest("Nieprawidłowe hasło.");

            // pliki audio w Blob Storage nie sa objete kaskadowym usuwaniem w SQL,
            // wiec trzeba je skasowac osobno, notatka po notatce
            var noteIds = await _db.Notes.Where(n => n.UserId == user.Id).Select(n => n.Id).ToListAsync();
            foreach (var noteId in noteIds)
                await _audioService.DeleteAllForNoteAsync(noteId);

            _db.Notes.RemoveRange(_db.Notes.Where(n => n.UserId == user.Id)); // cascade: Drawings/AudioClips
            _db.Devices.RemoveRange(_db.Devices.Where(d => d.UserId == user.Id));
            _db.PairingCodes.RemoveRange(_db.PairingCodes.Where(p => p.UserId == user.Id));
            _db.RefreshTokens.RemoveRange(_db.RefreshTokens.Where(t => t.UserId == user.Id));
            _db.PasswordResetTokens.RemoveRange(_db.PasswordResetTokens.Where(t => t.UserId == user.Id));
            _db.Users.Remove(user);

            await _db.SaveChangesAsync();

            Response.Cookies.Delete("refreshToken");
            return NoContent();
        }
    }
}
