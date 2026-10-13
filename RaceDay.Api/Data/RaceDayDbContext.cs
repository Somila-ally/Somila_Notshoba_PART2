using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data
{
    public class RaceDayDbContext : DbContext
    {
        public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Enrolment> Enrolments => Set<Enrolment>();
        public DbSet<Result> Results => Set<Result>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- Roles ---------------------------------------------------------
            modelBuilder.Entity<Role>()
                .HasIndex(r => r.RoleName)
                .IsUnique();

            modelBuilder.Entity<Role>().HasData(
                new Role { RoleId = 1, RoleName = RoleNames.Organiser },
                new Role { RoleId = 2, RoleName = RoleNames.Participant }
            );

            // --- Users ("Users" table) --------------------------------------------
            // UserId is not discovered by EF Core's default key naming convention for AppUser,
            // so declare it explicitly. This is required by both SQL Server and the InMemory
            // provider used by the integration tests.
            modelBuilder.Entity<AppUser>().HasKey(u => u.UserId);
            modelBuilder.Entity<AppUser>().ToTable("Users");

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // One Role -> many Users
            modelBuilder.Entity<AppUser>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Events ------------------------------------------------------------
            // Every relationship uses Restrict (SQL Server NO ACTION), matching the ERD/SQL script
            // (no ON DELETE CASCADE anywhere) and avoiding SQL Server's multiple-cascade-path error.
            // One Organiser -> many Events
            modelBuilder.Entity<Event>()
                .HasOne(e => e.Organiser)
                .WithMany(u => u.OrganisedEvents)
                .HasForeignKey(e => e.OrganiserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Event>()
                .Property(e => e.DistanceKm)
                .HasPrecision(5, 2);

            // --- Categories --------------------------------------------------------
            // One Event -> many Categories
            modelBuilder.Entity<Category>()
                .HasOne(c => c.Event)
                .WithMany(e => e.Categories)
                .HasForeignKey(c => c.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Category>()
                .Property(c => c.DistanceKm)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Category>()
                .Property(c => c.EntryFee)
                .HasPrecision(8, 2);

            // --- Enrolments ----------------------------------------------------------
            // One Participant -> many Enrolments
            modelBuilder.Entity<Enrolment>()
                .HasOne(en => en.Participant)
                .WithMany(u => u.Enrolments)
                .HasForeignKey(en => en.ParticipantId)
                .OnDelete(DeleteBehavior.Restrict);

            // One Event -> many Enrolments
            modelBuilder.Entity<Enrolment>()
                .HasOne(en => en.Event)
                .WithMany(e => e.Enrolments)
                .HasForeignKey(en => en.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            // One Category -> many Enrolments
            modelBuilder.Entity<Enrolment>()
                .HasOne(en => en.Category)
                .WithMany(c => c.Enrolments)
                .HasForeignKey(en => en.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Same category twice is not allowed (UQ_Enrolments_Participant_Category in the Part 1 SQL script).
            modelBuilder.Entity<Enrolment>()
                .HasIndex(en => new { en.ParticipantId, en.CategoryId })
                .IsUnique();

            // --- Results -------------------------------------------------------------
            // One Enrolment -> zero or one Result
            modelBuilder.Entity<Result>()
                .HasOne(r => r.Enrolment)
                .WithOne(en => en.Result)
                .HasForeignKey<Result>(r => r.EnrolmentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
