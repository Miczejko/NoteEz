using NoteEz_Server.Models;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;

namespace NoteEz_Server.Services
{
    // Application/Services/DevicePairingService.cs

    public class DevicePairingService
    {
        private readonly AppDbContext _db;

        public DevicePairingService(AppDbContext db) => _db = db;

        public async Task<PairInitResponse> InitPairingAsync(Guid userId)
        {
            var code = GenerateCode(); // np. 8 znaków, losowe, wykluczające mylące znaki (0/O, 1/l)
            var pairing = new PairingCode
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Used = false,
            };
            _db.PairingCodes.Add(pairing);
            await _db.SaveChangesAsync();
            return new PairInitResponse(code, pairing.ExpiresAt);
        }

        public async Task<ClaimDeviceResponse> ClaimAsync(ClaimDeviceRequest req)
        {
            var pairing = await _db.PairingCodes
                .FirstOrDefaultAsync(p => p.Code == req.Code && !p.Used && p.ExpiresAt > DateTime.UtcNow)
                ?? throw new InvalidOperationException("Nieprawidłowy lub wygasły kod parowania");

            var apiKey = GenerateApiKey(); // losowy, długi ciąg (np. 32 bajty base64)
            var device = new Device
            {
                Id = Guid.NewGuid(),
                UserId = pairing.UserId,
                Name = req.DeviceName,
                ApiKeyHash = Hash(apiKey),
                PairedAt = DateTime.UtcNow,
                Revoked = false
            };

            pairing.Used = true;
            _db.Devices.Add(device);
            await _db.SaveChangesAsync();

            return new ClaimDeviceResponse(apiKey); // klucz jawny, tylko teraz
        }

        public async Task<List<DeviceDto>> GetUserDevicesAsync(Guid userId)
        {
            return await _db.Devices
                .Where(d => d.UserId == userId)
                .Select(d => new DeviceDto(d.Id, d.Name, d.PairedAt, d.LastSeenAt, d.Revoked))
                .ToListAsync();
        }

        public async Task<bool> RevokeAsync(Guid userId, Guid deviceId)
        {
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId);
            if (device is null) return false;
            device.Revoked = true;
            await _db.SaveChangesAsync();
            return true;
        }

        private static string GenerateCode() =>
            RandomNumberGenerator.GetString("ABCDEFGHJKMNPQRSTUVWXYZ23456789", 8); // .NET 8+, bez mylących znaków

        private static string GenerateApiKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes);
        }

        private static string Hash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
    }
}
