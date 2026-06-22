using Microsoft.EntityFrameworkCore;
using WeddingOrchestrator.Api.Models;
using WeddingOrchestrator.Api.Models.Enums;

namespace WeddingOrchestrator.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<SongCategory> SongCategories => Set<SongCategory>();
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Wedding> Weddings => Set<Wedding>();
    public DbSet<WeddingRole> WeddingRoles => Set<WeddingRole>();
    public DbSet<WeddingRoleSongAssignment> WeddingRoleSongAssignments => Set<WeddingRoleSongAssignment>();
    public DbSet<PersonNote> PersonNotes => Set<PersonNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Person self-referencing FKs ────────────────────────────────────
        modelBuilder.Entity<Person>(e =>
        {
            e.HasOne(p => p.Father)
             .WithMany()
             .HasForeignKey(p => p.FatherId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.Mother)
             .WithMany()
             .HasForeignKey(p => p.MotherId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
            e.Property(p => p.LastName).HasMaxLength(100).IsRequired();
            e.Property(p => p.Gender).HasConversion<int>();
            e.Property(p => p.FamilyGroup).HasMaxLength(200);
            e.Ignore(p => p.FullName);
            e.HasIndex(p => new { p.FirstName, p.LastName }).IsUnique();
        });

        // ── SongCategory ───────────────────────────────────────────────────
        modelBuilder.Entity<SongCategory>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
        });

        // ── Song ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Song>(e =>
        {
            e.Property(s => s.Title).HasMaxLength(200).IsRequired();
            e.Property(s => s.RelativeFilePath).HasMaxLength(500).IsRequired();
        });

        // ── Wedding ────────────────────────────────────────────────────────
        modelBuilder.Entity<Wedding>(e =>
        {
            e.Property(w => w.Location).HasMaxLength(300);
        });

        // ── WeddingRole ────────────────────────────────────────────────────
        modelBuilder.Entity<WeddingRole>(e =>
        {
            e.HasIndex(r => new { r.WeddingId, r.RoleType }).IsUnique();
        });

        // ── PersonNote ─────────────────────────────────────────────────────
        modelBuilder.Entity<PersonNote>(e =>
        {
            e.Property(n => n.Content).HasMaxLength(2000).IsRequired();

            e.HasOne(n => n.Person)
             .WithMany()
             .HasForeignKey(n => n.PersonId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(n => n.Wedding)
             .WithMany()
             .HasForeignKey(n => n.WeddingId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── WeddingRoleSongAssignment ──────────────────────────────────────
        modelBuilder.Entity<WeddingRoleSongAssignment>(e =>
        {
            e.HasIndex(a => new { a.WeddingRoleId, a.AssignmentSlot }).IsUnique();

            e.HasOne(a => a.WeddingRole)
             .WithMany(r => r.SongAssignments)
             .HasForeignKey(a => a.WeddingRoleId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Song)
             .WithMany(s => s.Assignments)
             .HasForeignKey(a => a.SongId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Seed: SongCategories ───────────────────────────────────────────
        modelBuilder.Entity<SongCategory>().HasData(
            new SongCategory { Id = 1, Name = "Groom Songs", DisplayOrder = 1 },
            new SongCategory { Id = 2, Name = "Bride Songs", DisplayOrder = 2 },
            new SongCategory { Id = 3, Name = "Father Songs", DisplayOrder = 3 },
            new SongCategory { Id = 4, Name = "Mother Songs", DisplayOrder = 4 },
            new SongCategory { Id = 5, Name = "Grandmother Songs", DisplayOrder = 5 },
            new SongCategory { Id = 6, Name = "Grandfather Songs", DisplayOrder = 6 },
            new SongCategory { Id = 7, Name = "Intro for Father/Mother", DisplayOrder = 7 },
            new SongCategory { Id = 8, Name = "Wedding Intros",DisplayOrder = 8 }
        );
    }
}
