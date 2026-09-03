using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;
using NoteEz_Server.Models;

namespace NoteEz_Server.Services
{
    public class NotificationService
    {
        private readonly AppDbContext _db;

        public NotificationService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<NotificationDto>> GetMyNotificationsAsync(Guid userId)
        {
            return await _db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationDto(n.Id, n.Type, n.PayloadJson, n.IsRead, n.CreatedAt))
                .ToListAsync();
        }

        public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId)
        {
            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
            if (notification is null) return false;

            notification.IsRead = true;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task MarkAllReadAsync(Guid userId)
        {
            var notifications = await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in notifications) n.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }
}
