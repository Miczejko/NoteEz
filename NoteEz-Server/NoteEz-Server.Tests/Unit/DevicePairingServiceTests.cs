using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;
using NoteEz_Server.Models;
using NoteEz_Server.Services;
using Xunit;

namespace NoteEz_Server.Tests.Unit
{
    public class DevicePairingServiceTests
    {
        private static AppDbContext NewDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task InitPairingAsync_CreatesUnusedCodeExpiringInFuture()
        {
            await using var db = NewDb();
            var service = new DevicePairingService(db);
            var userId = Guid.NewGuid();

            var response = await service.InitPairingAsync(userId);

            var stored = await db.PairingCodes.SingleAsync();
            Assert.Equal(userId, stored.UserId);
            Assert.False(stored.Used);
            Assert.True(stored.ExpiresAt > DateTime.UtcNow);
            Assert.Equal(stored.Code, response.Code);
        }

        [Fact]
        public async Task ClaimAsync_ValidCode_CreatesDeviceAndMarksCodeUsed()
        {
            await using var db = NewDb();
            var service = new DevicePairingService(db);
            var userId = Guid.NewGuid();
            var pairing = await service.InitPairingAsync(userId);

            var claimResponse = await service.ClaimAsync(new ClaimDeviceRequest(pairing.Code, "ESP32-test"));

            Assert.False(string.IsNullOrEmpty(claimResponse.ApiKey));
            var device = await db.Devices.SingleAsync();
            Assert.Equal(userId, device.UserId);
            Assert.False(device.Revoked);

            var usedCode = await db.PairingCodes.SingleAsync();
            Assert.True(usedCode.Used);
        }

        [Fact]
        public async Task ClaimAsync_CodeUsedTwice_SecondAttemptThrows()
        {
            await using var db = NewDb();
            var service = new DevicePairingService(db);
            var pairing = await service.InitPairingAsync(Guid.NewGuid());

            await service.ClaimAsync(new ClaimDeviceRequest(pairing.Code, "device-1"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ClaimAsync(new ClaimDeviceRequest(pairing.Code, "device-2")));
        }

        [Fact]
        public async Task ClaimAsync_ExpiredCode_Throws()
        {
            await using var db = NewDb();
            var service = new DevicePairingService(db);
            var userId = Guid.NewGuid();

            db.PairingCodes.Add(new PairingCode
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Code = "EXPIRED1",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
                Used = false
            });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ClaimAsync(new ClaimDeviceRequest("EXPIRED1", "device")));
        }

        [Fact]
        public async Task ClaimAsync_UnknownCode_Throws()
        {
            await using var db = NewDb();
            var service = new DevicePairingService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ClaimAsync(new ClaimDeviceRequest("NOPE1234", "device")));
        }

        [Fact]
        public async Task RevokeAsync_OtherUsersDevice_ReturnsFalseAndDoesNotRevoke()
        {
            await using var db = NewDb();
            var service = new DevicePairingService(db);
            var owner = Guid.NewGuid();
            var attacker = Guid.NewGuid();
            var pairing = await service.InitPairingAsync(owner);
            await service.ClaimAsync(new ClaimDeviceRequest(pairing.Code, "device"));
            var device = await db.Devices.SingleAsync();

            var result = await service.RevokeAsync(attacker, device.Id);

            Assert.False(result);
            var stillActive = await db.Devices.SingleAsync();
            Assert.False(stillActive.Revoked);
        }

        [Fact]
        public async Task GetUserDevicesAsync_OnlyReturnsCallersDevices()
        {
            await using var db = NewDb();
            var service = new DevicePairingService(db);
            var userA = Guid.NewGuid();
            var userB = Guid.NewGuid();

            var pairingA = await service.InitPairingAsync(userA);
            await service.ClaimAsync(new ClaimDeviceRequest(pairingA.Code, "device-A"));
            var pairingB = await service.InitPairingAsync(userB);
            await service.ClaimAsync(new ClaimDeviceRequest(pairingB.Code, "device-B"));

            var devicesForA = await service.GetUserDevicesAsync(userA);

            var onlyDevice = Assert.Single(devicesForA);
            Assert.Equal("device-A", onlyDevice.Name);
        }
    }
}
