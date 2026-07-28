using Microsoft.Extensions.Configuration;
using NoteEz_Server.Services;

namespace NoteEz_Server.Tests.Integration
{
    // Podmienia prawdziwe wywolanie Cloudflare Turnstile /siteverify w testach -
    // zamiast sieci, po prostu akceptuje kazdy niepusty token.
    public class FakeTurnstileService : TurnstileService
    {
        public FakeTurnstileService() : base(new HttpClient(), new ConfigurationBuilder().Build())
        {
        }

        public override Task<bool> VerifyAsync(string? token, string? remoteIp)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(token));
        }
    }
}
