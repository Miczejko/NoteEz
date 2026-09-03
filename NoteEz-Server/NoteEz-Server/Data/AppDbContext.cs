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
        public DbSet<PendingRegistration> PendingRegistrations { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<GroupInvite> GroupInvites { get; set; }
        public DbSet<Notification> Notifications { get; set; }

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

            modelBuilder.Entity<Note>()
                .HasOne(n => n.Group)
                .WithMany(g => g.Notes)
                .HasForeignKey(n => n.GroupId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<GroupMember>()
                .HasIndex(m => new { m.GroupId, m.UserId })
                .IsUnique();

            modelBuilder.Entity<GroupMember>()
                .HasOne(m => m.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GroupMember>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GroupInvite>()
                .HasOne(i => i.Group)
                .WithMany(g => g.Invites)
                .HasForeignKey(i => i.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GroupInvite>()
                .HasOne(i => i.InvitedUser)
                .WithMany()
                .HasForeignKey(i => i.InvitedUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }

    }
}
