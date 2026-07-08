using System.ComponentModel.DataAnnotations;

namespace NoteEz_Server.Models
{
    public class Device
    {
        [Key]
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string ApiKeyHash { get; set; } // SHA-256
        public DateTime PairedAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public bool Revoked { get; set; }
    }

    public record DeviceDto(Guid Id, string Name, DateTime PairedAt, DateTime? LastSeenAt, bool Revoked);

}
