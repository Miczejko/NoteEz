using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoteEz_Server.Data;
using NoteEz_Server.Services;

namespace NoteEz_Server.Tests.Integration
{
    // Uruchamia cala aplikacje (Program.cs) w pamieci, ale podmienia dwie zaleznosci
    // sieciowe na bezpieczne dla testow: SQL Server -> EF Core InMemory, oraz
    // prawdziwy Azure Blob Storage -> nieuzywany (leniwie tworzony) klient blobow,
    // zeby testy nie potrzebowaly ani bazy danych, ani konta Azure.
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        public readonly string DbName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "test-only-signing-key-not-a-real-secret-1234567890",
                    ["Jwt:Issuer"] = "NoteEzTests",
                    ["Jwt:Audience"] = "NoteEzTests",
                    ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;Trusted_Connection=True;",
                    ["ConnectionStrings:BlobStorage"] = "UseDevelopmentStorage=true",
                    ["BlobStorage:ContainerName"] = "test-audio",
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(DbName));

                // Konstruowanie BlobContainerClient nie laczy sie z siecia - tylko wywolanie
                // operacji (np. UploadAsync) by to zrobilo, a zadny test tego nie wywoluje.
                services.RemoveAll<BlobContainerClient>();
                services.AddSingleton(_ =>
                    new BlobServiceClient("UseDevelopmentStorage=true").GetBlobContainerClient("test-audio"));

                // Prawdziwy EmailService (SendGrid) wymagalby sieci i prawdziwego API key -
                // podmieniamy na fake, ktory zapisuje linki z maili do TestEmailCapture.
                services.RemoveAll<EmailService>();
                services.AddScoped<EmailService, FakeEmailService>();

                // Prawdziwy TurnstileService wywolywalby siec (Cloudflare /siteverify) -
                // podmieniamy na fake, ktory akceptuje kazdy niepusty token.
                services.RemoveAll<TurnstileService>();
                services.AddScoped<TurnstileService, FakeTurnstileService>();
            });
        }
    }
}
