using System.ComponentModel.DataAnnotations;


namespace NoteEz_Server.Models
{
    public class Note
    {
        [Key]
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public string? TextContent { get; set; }  
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public User User { get; set; }
        public ICollection<NoteDrawing> Drawings { get; set; } = new List<NoteDrawing>();
        public ICollection<NoteAudio> AudioClips { get; set; } = new List<NoteAudio>();
    }

    public record NoteDto(
        Guid Id,
        string Title,
        string? TextContent,
        DateTime UpdatedAt,
        IReadOnlyList<NoteDrawingDto> Drawings,
        IReadOnlyList<NoteAudioDto> AudioClips
    );

    public record NoteLiteDto(Guid Id, string Title, bool HasDrawing, bool HasAudio);

    public record CreateNoteRequest(string Title, string? TextContent);
    public record UpdateNoteRequest(string? Title, string? TextContent);
    public record AddDrawingRequest(string StrokesJson);
    public record ReorderRequest(List<Guid> OrderedIds);
}
