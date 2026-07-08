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

    public record RegisterDto(string Username, string Password);
    public record LoginDto(string Username, string Password);
}
