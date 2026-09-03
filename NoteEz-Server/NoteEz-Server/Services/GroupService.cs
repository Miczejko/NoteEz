using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;
using NoteEz_Server.Models;

namespace NoteEz_Server.Services
{
    public record InviteResult(GroupInviteDto Invite, Guid? InvitedUserId, Notification? Notification);

    public class GroupService
    {
        private readonly AppDbContext _db;

        public GroupService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<GroupDto> CreateAsync(Guid userId, CreateGroupRequest req)
        {
            var group = new Group
            {
                Id = Guid.NewGuid(),
                Name = req.Name,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            var member = new GroupMember
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                UserId = userId,
                Role = GroupRole.Owner,
                JoinedAt = DateTime.UtcNow
            };

            _db.Groups.Add(group);
            _db.GroupMembers.Add(member);
            await _db.SaveChangesAsync();

            return new GroupDto(group.Id, group.Name, group.CreatedAt, 1, GroupRole.Owner.ToString());
        }

        public async Task<List<GroupDto>> GetMyGroupsAsync(Guid userId)
        {
            var memberships = await _db.GroupMembers
                .Where(m => m.UserId == userId)
                .Include(m => m.Group)
                .ToListAsync();

            var groupIds = memberships.Select(m => m.GroupId).ToList();
            var counts = await _db.GroupMembers
                .Where(m => groupIds.Contains(m.GroupId))
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToListAsync();
            var countMap = counts.ToDictionary(c => c.GroupId, c => c.Count);

            return memberships
                .Select(m => new GroupDto(
                    m.Group.Id,
                    m.Group.Name,
                    m.Group.CreatedAt,
                    countMap.GetValueOrDefault(m.GroupId),
                    m.Role.ToString()))
                .ToList();
        }

        public async Task<List<GroupMemberDto>> GetMembersAsync(Guid userId, Guid groupId)
        {
            await EnsureMemberAsync(userId, groupId);

            return await _db.GroupMembers
                .Where(m => m.GroupId == groupId)
                .Include(m => m.User)
                .Select(m => new GroupMemberDto(m.UserId, m.User.Username, m.Role.ToString(), m.JoinedAt))
                .ToListAsync();
        }

        public async Task<InviteResult> InviteAsync(Guid userId, Guid groupId, InviteToGroupRequest req)
        {
            var caller = await _db.GroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId)
                ?? throw new UnauthorizedAccessException("Nie jesteś członkiem tej grupy.");

            if (caller.Role != GroupRole.Owner)
                throw new UnauthorizedAccessException("Tylko właściciel grupy może zapraszać.");

            var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId)
                ?? throw new KeyNotFoundException("Grupa nie znaleziona.");

            var invitedUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == req.Username)
                ?? throw new KeyNotFoundException("Nie znaleziono użytkownika o podanej nazwie.");

            if (await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == invitedUser.Id))
                throw new InvalidOperationException("Ten użytkownik jest już członkiem grupy.");

            if (await _db.GroupInvites.AnyAsync(i => i.GroupId == groupId && i.InvitedUserId == invitedUser.Id && i.Status == GroupInviteStatus.Pending))
                throw new InvalidOperationException("Ten użytkownik ma już oczekujące zaproszenie do tej grupy.");

            var invitedByUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

            var invite = new GroupInvite
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                InvitedUserId = invitedUser.Id,
                InvitedByUserId = userId,
                Status = GroupInviteStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
            _db.GroupInvites.Add(invite);

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = invitedUser.Id,
                Type = "GroupInvite",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    inviteId = invite.Id,
                    groupId = group.Id,
                    groupName = group.Name,
                    invitedByUsername = invitedByUser?.Username
                }),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _db.Notifications.Add(notification);

            await _db.SaveChangesAsync();

            var dto = new GroupInviteDto(invite.Id, group.Id, group.Name, invitedUser.Username, invitedByUser?.Username ?? "", invite.CreatedAt);
            return new InviteResult(dto, invitedUser.Id, notification);
        }

        public async Task<GroupMemberDto> RespondToInviteAsync(Guid userId, Guid inviteId, bool accept)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new KeyNotFoundException("Użytkownik nie znaleziony.");

            var invite = await _db.GroupInvites.FirstOrDefaultAsync(i => i.Id == inviteId)
                ?? throw new KeyNotFoundException("Zaproszenie nie znalezione.");

            if (invite.InvitedUserId != user.Id)
                throw new UnauthorizedAccessException("To zaproszenie nie dotyczy Twojego konta.");

            if (invite.Status != GroupInviteStatus.Pending)
                throw new InvalidOperationException("Zaproszenie zostało już rozpatrzone.");

            if (invite.ExpiresAt < DateTime.UtcNow)
            {
                invite.Status = GroupInviteStatus.Expired;
                await _db.SaveChangesAsync();
                throw new InvalidOperationException("Zaproszenie wygasło.");
            }

            invite.Status = accept ? GroupInviteStatus.Accepted : GroupInviteStatus.Declined;

            GroupMemberDto? result = null;
            if (accept)
            {
                var existing = await _db.GroupMembers
                    .FirstOrDefaultAsync(m => m.GroupId == invite.GroupId && m.UserId == userId);

                if (existing is null)
                {
                    var member = new GroupMember
                    {
                        Id = Guid.NewGuid(),
                        GroupId = invite.GroupId,
                        UserId = userId,
                        Role = GroupRole.Member,
                        JoinedAt = DateTime.UtcNow
                    };
                    _db.GroupMembers.Add(member);
                    result = new GroupMemberDto(userId, user.Username, member.Role.ToString(), member.JoinedAt);
                }
                else
                {
                    result = new GroupMemberDto(userId, user.Username, existing.Role.ToString(), existing.JoinedAt);
                }
            }

            await _db.SaveChangesAsync();
            return result ?? new GroupMemberDto(userId, user.Username, GroupRole.Member.ToString(), DateTime.UtcNow);
        }

        public async Task LeaveGroupAsync(Guid userId, Guid groupId)
        {
            var member = await _db.GroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId)
                ?? throw new KeyNotFoundException("Nie jesteś członkiem tej grupy.");

            if (member.Role == GroupRole.Owner)
            {
                var otherOwners = await _db.GroupMembers
                    .CountAsync(m => m.GroupId == groupId && m.Role == GroupRole.Owner && m.UserId != userId);

                if (otherOwners == 0)
                    throw new InvalidOperationException("Jesteś jedynym właścicielem grupy - najpierw przekaż własność lub usuń grupę.");
            }

            _db.GroupMembers.Remove(member);
            await _db.SaveChangesAsync();
        }

        public async Task RemoveMemberAsync(Guid userId, Guid groupId, Guid memberUserId)
        {
            var caller = await _db.GroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId)
                ?? throw new UnauthorizedAccessException("Nie jesteś członkiem tej grupy.");

            if (caller.Role != GroupRole.Owner)
                throw new UnauthorizedAccessException("Tylko właściciel grupy może usuwać członków.");

            var target = await _db.GroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == memberUserId)
                ?? throw new KeyNotFoundException("Członek nie znaleziony.");

            _db.GroupMembers.Remove(target);
            await _db.SaveChangesAsync();
        }

        public async Task<List<GroupInviteDto>> GetPendingInvitesForUserAsync(Guid userId)
        {
            return await _db.GroupInvites
                .Where(i => i.InvitedUserId == userId && i.Status == GroupInviteStatus.Pending)
                .Include(i => i.Group)
                .Include(i => i.InvitedUser)
                .Select(i => new GroupInviteDto(
                    i.Id,
                    i.GroupId,
                    i.Group.Name,
                    i.InvitedUser.Username,
                    _db.Users.Where(u => u.Id == i.InvitedByUserId).Select(u => u.Username).FirstOrDefault() ?? "",
                    i.CreatedAt))
                .ToListAsync();
        }

        public async Task EnsureMemberAsync(Guid userId, Guid groupId)
        {
            var isMember = await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
            if (!isMember)
                throw new UnauthorizedAccessException("Nie jesteś członkiem tej grupy.");
        }
    }
}
