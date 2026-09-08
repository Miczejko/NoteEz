using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NoteEz_Server.Services
{
    public class EmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public EmailService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;

            _httpClient.BaseAddress ??= new Uri("https://api.resend.com/");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config["Resend:ApiKey"]);
        }

        public virtual Task SendVerificationEmailAsync(string toEmail, string verificationLink)
        {
            return SendButtonEmailAsync(
                toEmail,
                subject: "Potwierdź swój adres e-mail - NoteEz",
                heading: "Cześć!",
                bodyHtml: "Aby dokończyć zakładanie konta w NoteEz, potwierdź swój adres e-mail.",
                buttonText: "Potwierdź adres e-mail",
                link: verificationLink,
                footnote: "Link jest ważny przez 24 godziny. Jeśli to nie Ty próbowałeś/aś założyć konto, zignoruj tę wiadomość.");
        }

        public virtual Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
        {
            return SendButtonEmailAsync(
                toEmail,
                subject: "Zmiana hasła - NoteEz",
                heading: "Prośba o zmianę hasła",
                bodyHtml: "Otrzymaliśmy prośbę o zmianę hasła do Twojego konta NoteEz.",
                buttonText: "Zmień hasło",
                link: resetLink,
                footnote: "Link jest ważny przez 1 godzinę. Jeśli to nie Ty prosiłeś/aś o zmianę hasła, zignoruj tę wiadomość - Twoje hasło pozostanie bez zmian.");
        }

        public virtual async Task SendRegistrationAttemptOnExistingAccountEmailAsync(string toEmail)
        {
            var text = "Ktoś próbował założyć konto NoteEz używając Twojego adresu e-mail, ale masz już u nas konto. " +
                       "Jeśli to byłeś/aś Ty, po prostu się zaloguj. Jeśli zapomniałeś/aś hasła, skorzystaj z opcji " +
                       "\"Nie pamiętam hasła\" na stronie logowania. Jeśli to nie Ty, zignoruj tę wiadomość.";

            var htmlContent = $"""
                <div style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 480px; margin: 0 auto;">
                  <p>{text}</p>
                </div>
                """;

            await SendEmailAsync(toEmail, "Próba założenia konta - NoteEz", text, htmlContent);
        }

        private Task SendButtonEmailAsync(
            string toEmail, string subject, string heading, string bodyHtml, string buttonText, string link, string footnote)
        {
            var htmlContent = $"""
                <div style="font-family: 'Segoe UI', Arial, sans-serif; max-width: 480px; margin: 0 auto;">
                  <p>{heading}</p>
                  <p>{bodyHtml}</p>
                  <p style="text-align: center; margin: 2rem 0;">
                    <a href="{link}"
                       style="background: #79c7c5; color: #000501; text-decoration: none; font-weight: 600;
                              padding: 0.875rem 1.75rem; border-radius: 8px; display: inline-block;">
                      {buttonText}
                    </a>
                  </p>
                  <p style="color: #666; font-size: 0.875rem;">
                    Jeśli przycisk nie działa, skopiuj i wklej ten link do przeglądarki:<br />
                    <a href="{link}">{link}</a>
                  </p>
                  <p style="color: #666; font-size: 0.8125rem;">{footnote}</p>
                </div>
                """;

            return SendEmailAsync(toEmail, subject, $"{bodyHtml} {buttonText}: {link}", htmlContent);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string text, string html)
        {
            var fromEmail = _config["Resend:FromEmail"];
            var fromName = _config["Resend:FromName"] ?? "NoteEz";

            var payload = new
            {
                from = $"{fromName} <{fromEmail}>",
                to = new[] { toEmail },
                subject,
                text,
                html
            };

            var response = await _httpClient.PostAsJsonAsync("emails", payload);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Resend error {response.StatusCode}: {body}");
            }
        }
    }
}
