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
    public DbSet<RelationshipType> RelationshipTypes => Set<RelationshipType>();
    public DbSet<PersonRelationship> PersonRelationships => Set<PersonRelationship>();

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

        // ── RelationshipType ───────────────────────────────────────────────
        modelBuilder.Entity<RelationshipType>(e =>
        {
            e.Property(r => r.TypeCode).HasMaxLength(50).IsRequired();
            e.Property(r => r.TypeLabel).HasMaxLength(100).IsRequired();
            e.Property(r => r.Category).HasMaxLength(20).IsRequired();
            e.HasIndex(r => r.TypeCode).IsUnique();
        });

        // ── PersonRelationship ─────────────────────────────────────────────
        modelBuilder.Entity<PersonRelationship>(e =>
        {
            e.HasOne(r => r.FromPerson).WithMany().HasForeignKey(r => r.FromPersonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.ToPerson).WithMany().HasForeignKey(r => r.ToPersonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.RelationshipType).WithMany(rt => rt.Relationships).HasForeignKey(r => r.RelationshipTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.FromPersonId, r.ToPersonId, r.RelationshipTypeId }).IsUnique();
        });

        // ── Seed: RelationshipTypes ────────────────────────────────────────
        modelBuilder.Entity<RelationshipType>().HasData(
            new RelationshipType { Id =  1, TypeCode = "FATHER",           TypeLabel = "Father",           Category = "DIRECT",   GenerationDelta =  1 },
            new RelationshipType { Id =  2, TypeCode = "MOTHER",           TypeLabel = "Mother",           Category = "DIRECT",   GenerationDelta =  1 },
            new RelationshipType { Id =  3, TypeCode = "SON",              TypeLabel = "Son",              Category = "DIRECT",   GenerationDelta = -1 },
            new RelationshipType { Id =  4, TypeCode = "DAUGHTER",         TypeLabel = "Daughter",         Category = "DIRECT",   GenerationDelta = -1 },
            new RelationshipType { Id =  5, TypeCode = "HUSBAND",          TypeLabel = "Husband",          Category = "DIRECT",   GenerationDelta =  0 },
            new RelationshipType { Id =  6, TypeCode = "WIFE",             TypeLabel = "Wife",             Category = "DIRECT",   GenerationDelta =  0 },
            new RelationshipType { Id =  7, TypeCode = "BROTHER",          TypeLabel = "Brother",          Category = "DIRECT",   GenerationDelta =  0 },
            new RelationshipType { Id =  8, TypeCode = "SISTER",           TypeLabel = "Sister",           Category = "DIRECT",   GenerationDelta =  0 },
            new RelationshipType { Id =  9, TypeCode = "GRANDFATHER",      TypeLabel = "Grandfather",      Category = "EXTENDED", GenerationDelta =  2 },
            new RelationshipType { Id = 10, TypeCode = "GRANDMOTHER",      TypeLabel = "Grandmother",      Category = "EXTENDED", GenerationDelta =  2 },
            new RelationshipType { Id = 11, TypeCode = "GRANDSON",         TypeLabel = "Grandson",         Category = "EXTENDED", GenerationDelta = -2 },
            new RelationshipType { Id = 12, TypeCode = "GRANDDAUGHTER",    TypeLabel = "Granddaughter",    Category = "EXTENDED", GenerationDelta = -2 },
            new RelationshipType { Id = 13, TypeCode = "UNCLE",            TypeLabel = "Uncle",            Category = "EXTENDED", GenerationDelta =  1 },
            new RelationshipType { Id = 14, TypeCode = "AUNT",             TypeLabel = "Aunt",             Category = "EXTENDED", GenerationDelta =  1 },
            new RelationshipType { Id = 15, TypeCode = "NEPHEW",           TypeLabel = "Nephew",           Category = "EXTENDED", GenerationDelta = -1 },
            new RelationshipType { Id = 16, TypeCode = "NIECE",            TypeLabel = "Niece",            Category = "EXTENDED", GenerationDelta = -1 },
            new RelationshipType { Id = 17, TypeCode = "COUSIN",           TypeLabel = "Cousin",           Category = "EXTENDED", GenerationDelta =  0 },
            new RelationshipType { Id = 18, TypeCode = "FATHER_IN_LAW",    TypeLabel = "Father-in-law",    Category = "INLAW",    GenerationDelta =  1 },
            new RelationshipType { Id = 19, TypeCode = "MOTHER_IN_LAW",    TypeLabel = "Mother-in-law",    Category = "INLAW",    GenerationDelta =  1 },
            new RelationshipType { Id = 20, TypeCode = "SON_IN_LAW",       TypeLabel = "Son-in-law",       Category = "INLAW",    GenerationDelta = -1 },
            new RelationshipType { Id = 21, TypeCode = "DAUGHTER_IN_LAW",  TypeLabel = "Daughter-in-law",  Category = "INLAW",    GenerationDelta = -1 },
            new RelationshipType { Id = 22, TypeCode = "BROTHER_IN_LAW",   TypeLabel = "Brother-in-law",   Category = "INLAW",    GenerationDelta =  0 },
            new RelationshipType { Id = 23, TypeCode = "SISTER_IN_LAW",    TypeLabel = "Sister-in-law",    Category = "INLAW",    GenerationDelta =  0 },
            new RelationshipType { Id = 24, TypeCode = "STEP_FATHER",      TypeLabel = "Step-father",      Category = "STEP",     GenerationDelta =  1 },
            new RelationshipType { Id = 25, TypeCode = "STEP_MOTHER",      TypeLabel = "Step-mother",      Category = "STEP",     GenerationDelta =  1 },
            new RelationshipType { Id = 26, TypeCode = "STEP_SON",         TypeLabel = "Step-son",         Category = "STEP",     GenerationDelta = -1 },
            new RelationshipType { Id = 27, TypeCode = "STEP_DAUGHTER",    TypeLabel = "Step-daughter",    Category = "STEP",     GenerationDelta = -1 },
            new RelationshipType { Id = 28, TypeCode = "STEP_BROTHER",     TypeLabel = "Step-brother",     Category = "STEP",     GenerationDelta =  0 },
            new RelationshipType { Id = 29, TypeCode = "STEP_SISTER",      TypeLabel = "Step-sister",      Category = "STEP",     GenerationDelta =  0 },
            new RelationshipType { Id = 30, TypeCode = "ADOPTIVE_FATHER",  TypeLabel = "Adoptive Father",  Category = "ADOPTED",  GenerationDelta =  1 },
            new RelationshipType { Id = 31, TypeCode = "ADOPTIVE_MOTHER",  TypeLabel = "Adoptive Mother",  Category = "ADOPTED",  GenerationDelta =  1 },
            new RelationshipType { Id = 32, TypeCode = "ADOPTED_SON",      TypeLabel = "Adopted Son",      Category = "ADOPTED",  GenerationDelta = -1 },
            new RelationshipType { Id = 33, TypeCode = "ADOPTED_DAUGHTER", TypeLabel = "Adopted Daughter", Category = "ADOPTED",  GenerationDelta = -1 },
            new RelationshipType { Id = 34, TypeCode = "HALF_BROTHER",          TypeLabel = "Half-brother",          Category = "HALF",  GenerationDelta =  0 },
            new RelationshipType { Id = 35, TypeCode = "HALF_SISTER",           TypeLabel = "Half-sister",           Category = "HALF",  GenerationDelta =  0 },
            new RelationshipType { Id = 36, TypeCode = "STEP_GRANDFATHER",      TypeLabel = "Step-grandfather",      Category = "STEP",  GenerationDelta =  2 },
            new RelationshipType { Id = 37, TypeCode = "STEP_GRANDMOTHER",      TypeLabel = "Step-grandmother",      Category = "STEP",  GenerationDelta =  2 },
            new RelationshipType { Id = 38, TypeCode = "STEP_GRANDSON",         TypeLabel = "Step-grandson",         Category = "STEP",  GenerationDelta = -2 },
            new RelationshipType { Id = 39, TypeCode = "STEP_GRANDDAUGHTER",    TypeLabel = "Step-granddaughter",    Category = "STEP",  GenerationDelta = -2 },
            new RelationshipType { Id = 40, TypeCode = "GRANDSON_IN_LAW",       TypeLabel = "Grandson-in-law",       Category = "INLAW", GenerationDelta = -2 },
            new RelationshipType { Id = 41, TypeCode = "GRANDDAUGHTER_IN_LAW",  TypeLabel = "Granddaughter-in-law",  Category = "INLAW", GenerationDelta = -2 },
            new RelationshipType { Id = 42, TypeCode = "GRANDFATHER_IN_LAW",    TypeLabel = "Grandfather-in-law",    Category = "INLAW", GenerationDelta =  2 },
            new RelationshipType { Id = 43, TypeCode = "GRANDMOTHER_IN_LAW",    TypeLabel = "Grandmother-in-law",    Category = "INLAW", GenerationDelta =  2 }
        );

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
