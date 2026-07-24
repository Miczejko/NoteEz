using System.ComponentModel.DataAnnotations;


namespace NoteEz_Server.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public ICollection<Note> Notes { get; set; }
        public ICollection<RefreshToken> RefreshTokens { get; set; }
    }

    public record RegisterDto(
        [Required, StringLength(32, MinimumLength = 3), RegularExpression(@"^[a-zA-Z0-9_.-]+$", ErrorMessage = "Nazwa użytkownika może zawierać tylko litery, cyfry, '_', '.', '-'.")]
        string Username,
        [Required, StringLength(100, MinimumLength = 8)]
        string Password);

    public record LoginDto(
        [Required, StringLength(32)]
        string Username,
        [Required, StringLength(100)]
        string Password);
}
