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
        public string? Color { get; set; } // akcent kafelka na liscie notatek, np. "#8963ba"
        public DateOnly? ScheduledDate { get; set; } // dzien w kalendarzu, do ktorego przypisana jest notatka
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
        string? Color,
        DateOnly? ScheduledDate,
        DateTime UpdatedAt,
        IReadOnlyList<NoteDrawingDto> Drawings,
        IReadOnlyList<NoteAudioDto> AudioClips
    );

    public record NoteLiteDto(Guid Id, string Title, bool HasDrawing, bool HasAudio, string? Color);

    public record CalendarNoteDto(Guid Id, string Title, string? TextContent, string? Color, DateOnly ScheduledDate);

    public record CreateNoteRequest(
        [Required, StringLength(200, MinimumLength = 1)]
        string Title,
        [StringLength(200_000)]
        string? TextContent,
        [RegularExpression(@"^#[0-9a-fA-F]{6}$")]
        string? Color,
        DateOnly? ScheduledDate);

    public record UpdateNoteRequest(
        [StringLength(200)]
        string? Title,
        [StringLength(200_000)]
        string? TextContent,
        [RegularExpression(@"^(#[0-9a-fA-F]{6})?$")]
        string? Color,
        DateOnly? ScheduledDate);

    public record AddDrawingRequest(
        [Required, StringLength(2_000_000)]
        string StrokesJson);

    public record ReorderRequest([Required] List<Guid> OrderedIds);
}
