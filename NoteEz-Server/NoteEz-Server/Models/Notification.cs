using System.ComponentModel.DataAnnotations;

namespace NoteEz_Server.Models
{
    public class Notification
    {
        [Key]
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Type { get; set; }
        public string PayloadJson { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public record NotificationDto(Guid Id, string Type, string PayloadJson, bool IsRead, DateTime CreatedAt);
}
