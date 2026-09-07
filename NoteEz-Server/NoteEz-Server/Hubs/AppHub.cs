using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;
using System.Security.Claims;

namespace NoteEz_Server.Hubs
{
    [Authorize]
    public class AppHub : Hub
    {
        private readonly AppDbContext _db;

        public AppHub(AppDbContext db)
        {
            _db = db;
        }

        private Guid UserId => Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string Username => Context.User!.Identity?.Name ?? "";

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{UserId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Grupy SignalR sa czyszczone automatycznie przy rozlaczeniu polaczenia,
            // wiec nie trzeba tu recznie usuwac z "user-{id}"/"note-{id}"/"group-{id}".
            // Celowo nie rozgloszamy "editing: false" tutaj - klient powinien wywolac
            // LeaveNoteEditing przy odmontowaniu edytora; nie chcemy sledzic, w ktorych
            // grupach nut/edycji znajdowalo sie kazde polaczenie.
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinNoteEditing(Guid noteId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"note-{noteId}");
            await Clients.OthersInGroup($"note-{noteId}")
                .SendAsync("EditorPresence", new { userId = UserId, username = Username, editing = true });
        }

        public async Task LeaveNoteEditing(Guid noteId)
        {
            await Clients.OthersInGroup($"note-{noteId}")
                .SendAsync("EditorPresence", new { userId = UserId, username = Username, editing = false });
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"note-{noteId}");
        }

        public async Task JoinGroupChannel(Guid groupId)
        {
            var isMember = await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == UserId);
            if (!isMember) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"group-{groupId}");
        }

        public async Task LeaveGroupChannel(Guid groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group-{groupId}");
        }
    }
}
