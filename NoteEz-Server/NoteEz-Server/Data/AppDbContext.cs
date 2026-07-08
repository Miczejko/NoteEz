using Microsoft.EntityFrameworkCore;
using NoteEz_Server.Models;

namespace NoteEz_Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) 
        { 
        }

        public DbSet<Note> Notes { get; set; }
        public DbSet<NoteAudio> NoteAudios { get; set; }
        public DbSet<NoteDrawing> NoteDrawings { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<PairingCode> PairingCodes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Note>()
                .HasMany(n => n.Drawings)
                .WithOne(i => i.Note)
                .HasForeignKey(i => i.NoteId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Note>()
                .HasMany(n => n.AudioClips)
                .WithOne(a => a.Note)
                .HasForeignKey(a => a.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
        }

    }
}
