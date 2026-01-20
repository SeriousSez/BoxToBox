using BoxToBox.Domain;
using BoxToBox.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoxToBox.Infrastructure;

public class BoxToBoxDbContext : IdentityDbContext<UserEntity, IdentityRole<Guid>, Guid>
{
    public BoxToBoxDbContext(DbContextOptions<BoxToBoxDbContext> options) : base(options)
    {
    }

    public DbSet<MatchEntity> Matches { get; set; }
    public DbSet<PlayerEntity> Players { get; set; }
    public DbSet<VideoAnalysisEntity> VideoAnalyses { get; set; }
    public DbSet<PlayerStatEntity> PlayerStats { get; set; }
    public DbSet<EventEntity> Events { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Match entity
        modelBuilder.Entity<MatchEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HomeTeam).IsRequired();
            entity.Property(e => e.AwayTeam).IsRequired();
            entity.HasMany(e => e.Players).WithMany(p => p.Matches);
            entity.HasMany(e => e.VideoAnalyses)
                .WithOne(v => v.Match)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Player entity
        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasMany(e => e.PlayerStats)
                .WithOne(ps => ps.Player)
                .HasForeignKey(ps => ps.PlayerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure VideoAnalysis entity
        modelBuilder.Entity<VideoAnalysisEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VideoFileName).IsRequired();
            entity.Property(e => e.VideoPath).IsRequired();
            entity.HasMany(e => e.PlayerStats)
                .WithOne(ps => ps.VideoAnalysis)
                .HasForeignKey(ps => ps.VideoAnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Events)
                .WithOne(evt => evt.VideoAnalysis)
                .HasForeignKey(evt => evt.VideoAnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure PlayerStat entity
        modelBuilder.Entity<PlayerStatEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerName).IsRequired();
            entity.Property(e => e.Team).IsRequired();
        });

        // Configure Event entity
        modelBuilder.Entity<EventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerName).IsRequired();
            entity.Property(e => e.Team).IsRequired();
            entity.Property(e => e.Details).HasMaxLength(500);
        });

        // Deterministic Identity seed for admin user
        var adminRoleId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa7");
        var adminUserId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

        modelBuilder.Entity<IdentityRole<Guid>>().HasData(
            new IdentityRole<Guid>
            {
                Id = adminRoleId,
                Name = "ADMIN",
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "c8f2c4db-6a43-4f5e-8d8a-0d8f1b5d9b8a"
            }
        );

        var hasher = new PasswordHasher<UserEntity>();
        var adminUser = new UserEntity
        {
            Id = adminUserId,
            UserName = "Sez",
            NormalizedUserName = "SEZ",
            Email = "moyumbnm@hotmail.com",
            NormalizedEmail = "MOYUMBNM@HOTMAIL.COM",
            EmailConfirmed = true,
            FirstName = "Sezgin",
            LastName = "Sahin",
            SecurityStamp = "d1c7b2df-7c5c-44a7-8c6c-4f5a8d1c9e7b",
            ConcurrencyStamp = "a4e5b6c7-d8e9-4f0a-b1c2-d3e4f5a6b7c8",
            Created = new DateTime(2024, 01, 01, 00, 00, 00, DateTimeKind.Utc),
            Modified = new DateTime(2024, 01, 01, 00, 00, 00, DateTimeKind.Utc)
        };
        adminUser.PasswordHash = hasher.HashPassword(adminUser, "1234");

        modelBuilder.Entity<UserEntity>().HasData(adminUser);

        modelBuilder.Entity<IdentityUserRole<Guid>>().HasData(
            new IdentityUserRole<Guid>
            {
                RoleId = adminRoleId,
                UserId = adminUserId
            }
        );
    }
}
