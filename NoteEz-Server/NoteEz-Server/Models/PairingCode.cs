using System.ComponentModel.DataAnnotations;

namespace NoteEz_Server.Models
{
    public class PairingCode
    {
        [Key]
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Code { get; set; } // krótki, jednorazowy
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; }

    }

    public record PairInitResponse(string Code, DateTime ExpiresAt);

    public record ClaimDeviceRequest(
        [Required, StringLength(16, MinimumLength = 4)]
        string Code,
        [Required, StringLength(64, MinimumLength = 1)]
        string DeviceName);

    public record ClaimDeviceResponse(string ApiKey);
}
