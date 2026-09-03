using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using NoteEz_Server.Hubs;
using NoteEz_Server.Models;
using NoteEz_Server.Services;
using System.Security.Claims;

namespace NoteEz_Server.Controllers
{
    [ApiController]
    [Route("api/groups")]
    [Authorize]
    public class GroupsController : ControllerBase
    {
        private readonly GroupService _groups;
        private readonly IHubContext<AppHub> _hub;

        public GroupsController(GroupService groups, IHubContext<AppHub> hub)
        {
            _groups = groups;
            _hub = hub;
        }

        private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetMyGroups() => Ok(await _groups.GetMyGroupsAsync(UserId));

        [HttpPost]
        public async Task<IActionResult> Create(CreateGroupRequest req)
        {
            var group = await _groups.CreateAsync(UserId, req);
            return CreatedAtAction(nameof(GetMyGroups), group);
        }

        [HttpGet("{id}/members")]
        public async Task<IActionResult> GetMembers(Guid id)
        {
            try
            {
                return Ok(await _groups.GetMembersAsync(UserId, id));
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpPost("{id}/invites")]
        [EnableRateLimiting("group-invites")]
        public async Task<IActionResult> Invite(Guid id, InviteToGroupRequest req)
        {
            try
            {
                var result = await _groups.InviteAsync(UserId, id, req);

                if (result.InvitedUserId is not null && result.Notification is not null)
                {
                    var dto = new NotificationDto(
                        result.Notification.Id,
                        result.Notification.Type,
                        result.Notification.PayloadJson,
                        result.Notification.IsRead,
                        result.Notification.CreatedAt);

                    await _hub.Clients.Group($"user-{result.InvitedUserId}")
                        .SendAsync("ReceiveNotification", dto);
                }

                return Ok(result.Invite);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpPost("invites/{inviteId}/respond")]
        public async Task<IActionResult> RespondToInvite(Guid inviteId, RespondToInviteRequest req)
        {
            try
            {
                var result = await _groups.RespondToInviteAsync(UserId, inviteId, req.Accept);
                return Ok(result);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpGet("invites/pending")]
        public async Task<IActionResult> GetPendingInvites()
        {
            try
            {
                return Ok(await _groups.GetPendingInvitesForUserAsync(UserId));
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpDelete("{id}/members/me")]
        public async Task<IActionResult> Leave(Guid id)
        {
            try
            {
                await _groups.LeaveGroupAsync(UserId, id);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        [HttpDelete("{id}/members/{memberUserId}")]
        public async Task<IActionResult> RemoveMember(Guid id, Guid memberUserId)
        {
            try
            {
                await _groups.RemoveMemberAsync(UserId, id, memberUserId);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }
    }
}
