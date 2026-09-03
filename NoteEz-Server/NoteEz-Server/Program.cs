using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NoteEz_Server.Data;
using NoteEz_Server.Services;
using NoteEz_Server.Hubs;
using System.Text;
using System.Threading.RateLimiting;
using NoteEz_Server.Authentication;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Ensure proper JSON serialization with camelCase naming
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        // Azure SQL Serverless usypia baze przy braku ruchu - pierwsze zapytanie po
        // przebudzeniu czesto dostaje przejsciowy blad 40613 ("not currently available").
        // EnableRetryOnFailure automatycznie ponawia takie bledy zamiast od razu wywalac request.
        sqlOptions => sqlOptions.EnableRetryOnFailure()));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };

    // Przegladarki nie moga ustawic naglowka Authorization przy WebSocket upgrade,
    // wiec SignalR przesyla token jako query string - odczytujemy go tutaj tylko dla /hubs.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
})
.AddScheme<AuthenticationSchemeOptions, DeviceApiKeyHandler>(
    "DeviceApiKey", options => { });

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // wymagane, bo frontend woła z withCredentials: true (refresh token cookie)
    });
});

builder.Services.AddScoped<NoteService>();
builder.Services.AddScoped<NoteAudioService>();
builder.Services.AddScoped<DevicePairingService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddSignalR();
builder.Services.AddHttpClient<TurnstileService>();
builder.Services.AddSingleton(sp =>
{
    // Azure App Service blokuje Application Settings pod prefiksem "AzureBlobStorage"
    // (rezerwuje ten wzorzec nazwy dla wlasnej integracji ze Storage) - stad inna
    // nazwa sekcji. Sam connection string i tak idzie przez "Connection strings",
    // bo jego wartosc wyglada jak connection string (zawiera "AccountKey=").
    var connectionString = builder.Configuration.GetConnectionString("BlobStorage");
    var containerName = builder.Configuration["BlobStorage:ContainerName"];

    var blobServiceClient = new BlobServiceClient(connectionString);
    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

    // upewnij si�, �e kontener istnieje (przydatne przy pierwszym uruchomieniu / na nowym �rodowisku)
    containerClient.CreateIfNotExists(PublicAccessType.None);

    return containerClient;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var partitionKey = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.Identity.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous"
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("auth", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // Ogranicza spam zaproszeniami do grup - endpoint jest uwierzytelniony (JWT), wiec
    // partycjonujemy po userId, a nie po IP.
    options.AddPolicy("group-invites", httpContext =>
    {
        var partitionKey = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// naglowki ograniczajace typowe ataki po stronie przegladarki (clickjacking, MIME sniffing, wyciek referrera)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors("Frontend");

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();
app.MapHub<AppHub>("/hubs/app");

app.Run();

// Umozliwia WebApplicationFactory<Program> w projekcie testowym (NoteEz-Server.Tests)
// dostep do tej klasy Program wygenerowanej z top-level statements.
public partial class Program { }
