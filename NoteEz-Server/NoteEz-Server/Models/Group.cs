using System.ComponentModel.DataAnnotations;

namespace NoteEz_Server.Models
{
    public enum GroupRole
    {
        Owner,
        Member
    }

    public enum GroupInviteStatus
    {
        Pending,
        Accepted,
        Declined,
        Expired
    }

    public class Group
    {
        [Key]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedByUserId { get; set; }

        public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
        public ICollection<GroupInvite> Invites { get; set; } = new List<GroupInvite>();
        public ICollection<Note> Notes { get; set; } = new List<Note>();
    }

    public class GroupMember
    {
        [Key]
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }
        public GroupRole Role { get; set; }
        public DateTime JoinedAt { get; set; }

        public Group Group { get; set; }
        public User User { get; set; }
    }

    public class GroupInvite
    {
        [Key]
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public Guid InvitedUserId { get; set; }
        public Guid InvitedByUserId { get; set; }
        public GroupInviteStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        public Group Group { get; set; }
        public User InvitedUser { get; set; }
    }

    public record GroupDto(Guid Id, string Name, DateTime CreatedAt, int MemberCount, string Role);

    public record GroupMemberDto(Guid UserId, string Username, string Role, DateTime JoinedAt);

    public record GroupInviteDto(
        Guid Id,
        Guid GroupId,
        string GroupName,
        string InvitedUsername,
        string InvitedByUsername,
        DateTime CreatedAt);

    public record CreateGroupRequest(
        [Required, StringLength(100, MinimumLength = 1)]
        string Name);

    public record InviteToGroupRequest(
        [Required, StringLength(32, MinimumLength = 3)]
        string Username);

    public record RespondToInviteRequest(bool Accept);
}
