using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using NoteEz_Server.Services;

namespace NoteEz_Server.Tests.Integration
{
    // Podmienia prawdziwe wysylanie maili (Resend) w testach - zamiast tego zapisuje
    // linki z maili do statycznych slownikow, zeby testy mogly je odczytac (np. zeby
    // "kliknac" link weryfikacyjny bez prawdziwej skrzynki pocztowej).
    public class FakeEmailService : EmailService
    {
        public FakeEmailService(HttpClient httpClient, IConfiguration config) : base(httpClient, config)
        {
        }

        public override Task SendVerificationEmailAsync(string toEmail, string verificationLink)
        {
            TestEmailCapture.VerificationLinks[toEmail] = verificationLink;
            return Task.CompletedTask;
        }

        public override Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            TestEmailCapture.PasswordResetLinks[toEmail] = resetLink;
            return Task.CompletedTask;
        }

        public override Task SendRegistrationAttemptOnExistingAccountEmailAsync(string toEmail)
        {
            TestEmailCapture.RegistrationAttemptNotices.Add(toEmail);
            return Task.CompletedTask;
        }
    }

    public static class TestEmailCapture
    {
        public static readonly ConcurrentDictionary<string, string> VerificationLinks = new();
        public static readonly ConcurrentDictionary<string, string> PasswordResetLinks = new();
        public static readonly ConcurrentBag<string> RegistrationAttemptNotices = new();
    }
}
