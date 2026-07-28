using System.ComponentModel.DataAnnotations;


namespace NoteEz_Server.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public ICollection<Note> Notes { get; set; }
        public ICollection<RefreshToken> RefreshTokens { get; set; }
    }

    // Rejestracja jest dwuetapowa: konto (User) powstaje dopiero po kliknieciu
    // linku weryfikacyjnego. Do tego czasu dane czekaja tutaj.
    public class PendingRegistration
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string TokenHash { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    // Token do zmiany hasla - zarowno dla "nie pamietam hasla" (niezalogowany, podaje e-mail),
    // jak i zmiany hasla z panelu (zalogowany, link idzie na jego wlasny e-mail). Mechanizm ten sam.
    public class PasswordResetToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string TokenHash { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public record RegisterDto(
        [Required, StringLength(32, MinimumLength = 3), RegularExpression(@"^[a-zA-Z0-9_.-]+$", ErrorMessage = "Nazwa użytkownika może zawierać tylko litery, cyfry, '_', '.', '-'.")]
        string Username,
        [Required, EmailAddress, StringLength(256)]
        string Email,
        [Required, StringLength(100, MinimumLength = 8)]
        string Password);

    public record LoginDto(
        [Required, StringLength(32)]
        string Username,
        [Required, StringLength(100)]
        string Password);

    public record ForgotPasswordDto(
        [Required, EmailAddress, StringLength(256)]
        string Email);

    public record ResetPasswordDto(
        [Required]
        string Token,
        [Required, StringLength(100, MinimumLength = 8)]
        string NewPassword);
}
