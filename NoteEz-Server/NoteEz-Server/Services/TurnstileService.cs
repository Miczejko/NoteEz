using System.Text.Json.Serialization;

namespace NoteEz_Server.Services
{
    // Weryfikuje token z widgetu Cloudflare Turnstile (CAPTCHA) po stronie serwera -
    // token sam w sobie nic nie znaczy, dopiero /siteverify potwierdza, ze pochodzi
    // z prawdziwego rozwiazania widgetu, a nie np. z powtorki starej odpowiedzi.
    public class TurnstileService
    {
        private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public TurnstileService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public virtual async Task<bool> VerifyAsync(string? token, string? remoteIp)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var secretKey = _config["Turnstile:SecretKey"];
            if (string.IsNullOrEmpty(secretKey))
                return false;

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", secretKey),
                new KeyValuePair<string, string>("response", token),
                new KeyValuePair<string, string>("remoteip", remoteIp ?? ""),
            });

            using var response = await _httpClient.PostAsync(VerifyUrl, form);
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>();
            return result?.Success ?? false;
        }

        private class TurnstileResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }
        }
    }
}
