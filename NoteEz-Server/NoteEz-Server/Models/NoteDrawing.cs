using System.ComponentModel.DataAnnotations;


namespace NoteEz_Server.Models
{
    public class NoteDrawing
    {
        [Key]
        public Guid Id { get; set; }
        public Guid NoteId { get; set; }
        public string StrokesJson { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public Note Note { get; set; }
    }

    public record NoteDrawingDto(Guid Id, string StrokesJson, int SortOrder);
}
