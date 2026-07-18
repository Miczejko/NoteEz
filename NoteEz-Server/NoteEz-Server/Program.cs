using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NoteEz_Server.Data;
using NoteEz_Server.Services;
using System.Text;
using NoteEz_Server.Authentication;

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
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    Console.WriteLine($"Jwt:Key = '{builder.Configuration["Jwt:Key"]}'");

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
builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration["AzureBlobStorage:ConnectionString"];
    var containerName = builder.Configuration["AzureBlobStorage:ContainerName"];

    var blobServiceClient = new BlobServiceClient(connectionString);
    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

    // upewnij si�, �e kontener istnieje (przydatne przy pierwszym uruchomieniu / na nowym �rodowisku)
    containerClient.CreateIfNotExists(PublicAccessType.None);

    return containerClient;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthorization();

app.MapControllers();

app.MapGet("/api/diagnostics/blob-test", async (BlobContainerClient container) =>
{
    var exists = await container.ExistsAsync();
    return Results.Ok(new { containerExists = exists.Value });
});

app.Run();
