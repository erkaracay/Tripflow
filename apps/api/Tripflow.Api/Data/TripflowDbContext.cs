using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Data;

public sealed class TripflowDbContext : DbContext
{
    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<TourEntity> Tours => Set<TourEntity>();
    public DbSet<ParticipantEntity> Participants => Set<ParticipantEntity>();
    public DbSet<ParticipantAccessEntity> ParticipantAccesses => Set<ParticipantAccessEntity>();
    public DbSet<PortalSessionEntity> PortalSessions => Set<PortalSessionEntity>();
    public DbSet<TourPortalEntity> TourPortals => Set<TourPortalEntity>();
    public DbSet<CheckInEntity> CheckIns => Set<CheckInEntity>();

    public TripflowDbContext(DbContextOptions<TripflowDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationEntity>(b =>
        {
            b.ToTable("organizations");
            b.HasKey(x => x.Id);

            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Slug).HasMaxLength(64).IsRequired();
            b.Property(x => x.IsActive).IsRequired();
            b.Property(x => x.IsDeleted).IsRequired();
            b.Property(x => x.RequireLast4ForQr).IsRequired().HasDefaultValue(true);
            b.Property(x => x.RequireLast4ForPortal).IsRequired().HasDefaultValue(false);
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();

            b.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<UserEntity>(b =>
        {
            b.ToTable("users");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId);
            b.Property(x => x.Email).HasMaxLength(200).IsRequired();
            b.HasIndex(x => x.Email).IsUnique();

            b.Property(x => x.FullName).HasMaxLength(200);
            b.Property(x => x.PasswordHash).IsRequired();
            b.Property(x => x.Role).HasMaxLength(32).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasOne(x => x.Organization)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TourEntity>(b =>
        {
            b.ToTable("tours");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.StartDate).HasColumnType("date").IsRequired();
            b.Property(x => x.EndDate).HasColumnType("date").IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => new { x.OrganizationId, x.StartDate });

            b.HasOne(x => x.Organization)
                .WithMany(x => x.Tours)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.GuideUser)
                .WithMany(x => x.GuidedTours)
                .HasForeignKey(x => x.GuideUserId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasMany(x => x.Participants)
                .WithOne(x => x.Tour)
                .HasForeignKey(x => x.TourId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Portal)
                .WithOne(x => x.Tour)
                .HasForeignKey<TourPortalEntity>(x => x.TourId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParticipantEntity>(b =>
        {
            b.ToTable("participants");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.Phone).HasMaxLength(50);

            b.Property(x => x.CheckInCode).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.CheckInCode).IsUnique();

            b.Property(x => x.PortalFailedAttempts).IsRequired();
            b.Property(x => x.PortalLockedUntil);
            b.Property(x => x.PortalLastFailedAt);

            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => x.TourId);
            b.HasIndex(x => x.OrganizationId);

            b.HasOne(x => x.Organization)
                .WithMany(x => x.Participants)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParticipantAccessEntity>(b =>
        {
            b.ToTable("participant_access");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.ParticipantId).IsRequired();
            b.Property(x => x.Version).IsRequired();
            b.Property(x => x.Secret).HasMaxLength(128).IsRequired();
            b.Property(x => x.SecretHash).HasMaxLength(64).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.RevokedAt);

            b.HasIndex(x => x.OrganizationId);
            b.HasIndex(x => x.ParticipantId);

            b.HasOne(x => x.Organization)
                .WithMany(x => x.ParticipantAccesses)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Participant)
                .WithMany()
                .HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PortalSessionEntity>(b =>
        {
            b.ToTable("portal_sessions");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.ParticipantId).IsRequired();
            b.Property(x => x.ExpiresAt).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => x.OrganizationId);
            b.HasIndex(x => x.ParticipantId);

            b.HasOne(x => x.Organization)
                .WithMany(x => x.PortalSessions)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Participant)
                .WithMany()
                .HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TourPortalEntity>(b =>
        {
            b.ToTable("tour_portals");
            b.HasKey(x => x.TourId);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.PortalJson).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();

            b.HasIndex(x => x.OrganizationId);

            b.HasOne(x => x.Organization)
                .WithMany(x => x.Portals)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CheckInEntity>(b =>
        {
            b.ToTable("checkins");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.Method).HasMaxLength(16).IsRequired();
            b.Property(x => x.CheckedInAt).IsRequired();

            b.HasIndex(x => x.TourId);
            b.HasIndex(x => x.ParticipantId);
            b.HasIndex(x => x.OrganizationId);
            b.HasIndex(x => new { x.TourId, x.ParticipantId }).IsUnique();

            b.HasOne(x => x.Organization)
                .WithMany(x => x.CheckIns)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
