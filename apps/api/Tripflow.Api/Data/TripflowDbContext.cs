using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Data;

public sealed class TripflowDbContext : DbContext
{
    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<EventEntity> Events => Set<EventEntity>();
    public DbSet<ParticipantEntity> Participants => Set<ParticipantEntity>();
    public DbSet<ParticipantDetailsEntity> ParticipantDetails => Set<ParticipantDetailsEntity>();
    public DbSet<ParticipantAccessEntity> ParticipantAccesses => Set<ParticipantAccessEntity>();
    public DbSet<PortalSessionEntity> PortalSessions => Set<PortalSessionEntity>();
    public DbSet<EventPortalEntity> EventPortals => Set<EventPortalEntity>();
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
            b.Property(x => x.RequireLast4ForQr).IsRequired().HasDefaultValue(false);
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

        modelBuilder.Entity<EventEntity>(b =>
        {
            b.ToTable("events");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.StartDate).HasColumnType("date").IsRequired();
            b.Property(x => x.EndDate).HasColumnType("date").IsRequired();
            b.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => new { x.OrganizationId, x.StartDate });

            b.HasOne(x => x.Organization)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.GuideUser)
                .WithMany(x => x.GuidedEvents)
                .HasForeignKey(x => x.GuideUserId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasMany(x => x.Participants)
                .WithOne(x => x.Event)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Portal)
                .WithOne(x => x.Event)
                .HasForeignKey<EventPortalEntity>(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParticipantEntity>(b =>
        {
            b.ToTable("participants");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Phone).HasMaxLength(50).IsRequired();
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.TcNo).HasMaxLength(32).IsRequired();
            b.Property(x => x.BirthDate).HasColumnType("date").IsRequired();
            b.Property(x => x.Gender)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();

            b.Property(x => x.CheckInCode).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.CheckInCode).IsUnique();

            b.Property(x => x.PortalFailedAttempts).IsRequired();
            b.Property(x => x.PortalLockedUntil);
            b.Property(x => x.PortalLastFailedAt);

            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => x.EventId);
            b.HasIndex(x => x.OrganizationId);
            b.HasIndex(x => new { x.EventId, x.TcNo }).IsUnique();

            b.HasOne(x => x.Organization)
                .WithMany(x => x.Participants)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Details)
                .WithOne(x => x.Participant)
                .HasForeignKey<ParticipantDetailsEntity>(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParticipantDetailsEntity>(b =>
        {
            b.ToTable("participant_details", table =>
            {
                table.HasCheckConstraint(
                    "CK_participant_details_hotel_dates",
                    "\"HotelCheckOutDate\" IS NULL OR \"HotelCheckInDate\" IS NULL OR \"HotelCheckOutDate\" >= \"HotelCheckInDate\"");
            });
            b.HasKey(x => x.ParticipantId);

            b.Property(x => x.RoomNo).HasMaxLength(50);
            b.Property(x => x.RoomType).HasMaxLength(50);
            b.Property(x => x.PersonNo).HasMaxLength(50);
            b.Property(x => x.AgencyName).HasMaxLength(200);
            b.Property(x => x.City).HasMaxLength(100);
            b.Property(x => x.FlightCity).HasMaxLength(100);

            b.Property(x => x.HotelCheckInDate).HasColumnType("date");
            b.Property(x => x.HotelCheckOutDate).HasColumnType("date");

            b.Property(x => x.TicketNo).HasMaxLength(100);
            b.Property(x => x.AttendanceStatus).HasMaxLength(100);

            b.Property(x => x.ArrivalAirline).HasMaxLength(100);
            b.Property(x => x.ArrivalDepartureAirport).HasMaxLength(100);
            b.Property(x => x.ArrivalArrivalAirport).HasMaxLength(100);
            b.Property(x => x.ArrivalFlightCode).HasMaxLength(100);
            b.Property(x => x.ArrivalDepartureTime).HasColumnType("time without time zone");
            b.Property(x => x.ArrivalArrivalTime).HasColumnType("time without time zone");
            b.Property(x => x.ArrivalPnr).HasMaxLength(100);
            b.Property(x => x.ArrivalBaggageAllowance).HasMaxLength(100);

            b.Property(x => x.ReturnAirline).HasMaxLength(100);
            b.Property(x => x.ReturnDepartureAirport).HasMaxLength(100);
            b.Property(x => x.ReturnArrivalAirport).HasMaxLength(100);
            b.Property(x => x.ReturnFlightCode).HasMaxLength(100);
            b.Property(x => x.ReturnDepartureTime).HasColumnType("time without time zone");
            b.Property(x => x.ReturnArrivalTime).HasColumnType("time without time zone");
            b.Property(x => x.ReturnPnr).HasMaxLength(100);
            b.Property(x => x.ReturnBaggageAllowance).HasMaxLength(100);

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

        modelBuilder.Entity<EventPortalEntity>(b =>
        {
            b.ToTable("event_portals");
            b.HasKey(x => x.EventId);

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

            b.HasIndex(x => x.EventId);
            b.HasIndex(x => x.ParticipantId);
            b.HasIndex(x => x.OrganizationId);
            b.HasIndex(x => new { x.EventId, x.ParticipantId }).IsUnique();

            b.HasOne(x => x.Organization)
                .WithMany(x => x.CheckIns)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
