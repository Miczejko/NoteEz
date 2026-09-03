using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NoteEz_Server.Services;
using System.Security.Claims;

namespace NoteEz_Server.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly NotificationService _notifications;

        public NotificationsController(NotificationService notifications)
        {
            _notifications = notifications;
        }

        private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _notifications.GetMyNotificationsAsync(UserId));

        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkRead(Guid id)
            => await _notifications.MarkReadAsync(UserId, id) ? NoContent() : NotFound();

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            await _notifications.MarkAllReadAsync(UserId);
            return NoContent();
        }
    }
}
