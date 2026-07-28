using SendGrid;
using SendGrid.Helpers.Mail;

namespace NoteEz_Server.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public Task SendVerificationEmailAsync(string toEmail, string verificationLink)
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

        public Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
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

        private async Task SendButtonEmailAsync(
            string toEmail, string subject, string heading, string bodyHtml, string buttonText, string link, string footnote)
        {
            var apiKey = _config["SendGrid:ApiKey"];
            var fromEmail = _config["SendGrid:FromEmail"];
            var fromName = _config["SendGrid:FromName"] ?? "NoteEz";

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress(fromEmail, fromName);
            var to = new EmailAddress(toEmail);

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

            var msg = MailHelper.CreateSingleEmail(
                from, to, subject,
                plainTextContent: $"{bodyHtml} {buttonText}: {link}",
                htmlContent: htmlContent);

            var response = await client.SendEmailAsync(msg);
            if ((int)response.StatusCode >= 400)
            {
                var body = await response.Body.ReadAsStringAsync();
                throw new InvalidOperationException($"SendGrid error {response.StatusCode}: {body}");
            }
        }
    }
}
