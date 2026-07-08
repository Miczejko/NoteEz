using System.ComponentModel.DataAnnotations;


namespace NoteEz_Server.Models
{
    public class NoteAudio
    {
        public Guid Id { get; set; }
        public Guid NoteId { get; set; }
        public string BlobUrl { get; set; }
        public int DurationSeconds { get; set; }
        public long FileSizeBytes { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }

        public Note Note { get; set; }
    }

    public record NoteAudioDto(Guid Id, string Url, int DurationSeconds, int SortOrder);

}
