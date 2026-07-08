using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using NoteEz_Server.Models;
using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Data;

namespace NoteEz_Server.Services
{
    // Application/Services/NoteAudioService.cs

    public class NoteAudioService
    {
        private readonly AppDbContext _db;
        private readonly BlobContainerClient _blobContainer; // wstrzyknięty przez DI, skonfigurowany w Program.cs

        public NoteAudioService(AppDbContext db, BlobContainerClient blobContainer)
        {
            _db = db;
            _blobContainer = blobContainer;
        }

        public async Task<NoteAudioDto> AddAsync(Guid userId, Guid noteId, Stream audioStream, string contentType, long fileSizeBytes, int durationSeconds)
        {
            var note = await _db.Notes
                .Include(n => n.AudioClips)
                .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId)
                ?? throw new KeyNotFoundException("Notatka nie znaleziona");

            Guid audioGuid = Guid.NewGuid();
            var blobName = $"{noteId}/{audioGuid}.webm";
            var blobClient = _blobContainer.GetBlobClient(blobName);
            await blobClient.UploadAsync(audioStream, new BlobHttpHeaders { ContentType = contentType });

            var audio = new NoteAudio
            {
                Id = audioGuid,
                NoteId = noteId,
                BlobUrl = blobClient.Uri.ToString(),
                DurationSeconds = durationSeconds,
                FileSizeBytes = fileSizeBytes,
                SortOrder = note.AudioClips.Count,
                CreatedAt = DateTime.UtcNow
            };

            _db.NoteAudios.Add(audio);
            note.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return new NoteAudioDto(audio.Id, audio.BlobUrl, audio.DurationSeconds, audio.SortOrder);
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid noteId, Guid audioId)
        {
            var audio = await _db.NoteAudios
                .FirstOrDefaultAsync(a => a.Id == audioId && a.NoteId == noteId && a.Note.UserId == userId);

            if (audio is null) return false;

            var blobName = $"{noteId}/{audioId}.webm";
            await _blobContainer.GetBlobClient(blobName).DeleteIfExistsAsync();

            _db.NoteAudios.Remove(audio);
            await _db.SaveChangesAsync();
            return true;
        }

      

        // wywoływane z NoteService.DeleteAsync przed skasowaniem notatki
        public async Task DeleteAllForNoteAsync(Guid noteId)
        {
            var clips = await _db.NoteAudios.Where(a => a.NoteId == noteId).ToListAsync();
            foreach (var clip in clips)
            {
                var blobName = $"{noteId}/{clip.Id}.webm";
                await _blobContainer.GetBlobClient(blobName).DeleteIfExistsAsync();
            }
        }

        public async Task<(Stream stream, string contentType)?> GetAudioStreamAsync(Guid userId, Guid noteId, Guid audioId)
        {
            var audio = await _db.NoteAudios
                .FirstOrDefaultAsync(a => a.Id == audioId && a.NoteId == noteId && a.Note.UserId == userId);

            if (audio is null) return null;

            var blobName = $"{noteId}/{audioId}.webm"; // Poprawna ścieżka do bloba w kontenerze

            var blobClient = _blobContainer.GetBlobClient(blobName);

            var download = await blobClient.DownloadStreamingAsync();
            return (download.Value.Content, download.Value.Details.ContentType);
        }
    }
}
