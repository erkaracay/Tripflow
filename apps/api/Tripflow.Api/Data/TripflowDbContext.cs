using Microsoft.EntityFrameworkCore;
using Tripflow.Api.Data.Entities;

namespace Tripflow.Api.Data;

public sealed class TripflowDbContext : DbContext
{
    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<EventEntity> Events => Set<EventEntity>();
    public DbSet<EventDayEntity> EventDays => Set<EventDayEntity>();
    public DbSet<EventActivityEntity> EventActivities => Set<EventActivityEntity>();
    public DbSet<ParticipantEntity> Participants => Set<ParticipantEntity>();
    public DbSet<ParticipantDetailsEntity> ParticipantDetails => Set<ParticipantDetailsEntity>();
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
            b.Property(x => x.EventAccessCode).HasMaxLength(16).IsRequired();
            b.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => new { x.OrganizationId, x.StartDate });
            b.HasIndex(x => x.EventAccessCode).IsUnique();

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

        modelBuilder.Entity<EventDayEntity>(b =>
        {
            b.ToTable("event_days");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.EventId).IsRequired();
            b.Property(x => x.Date).HasColumnType("date").IsRequired();
            b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.SortOrder).IsRequired();
            b.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

            b.HasIndex(x => new { x.OrganizationId, x.EventId, x.Date });
            b.HasIndex(x => new { x.OrganizationId, x.EventId, x.SortOrder });

            b.HasOne(x => x.Organization)
                .WithMany(x => x.EventDays)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Event)
                .WithMany(x => x.Days)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.Activities)
                .WithOne(x => x.Day)
                .HasForeignKey(x => x.EventDayId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventActivityEntity>(b =>
        {
            b.ToTable("event_activities", table =>
            {
                table.HasCheckConstraint(
                    "CK_event_activities_time_range",
                    "\"EndTime\" IS NULL OR \"StartTime\" IS NULL OR \"EndTime\" >= \"StartTime\"");
            });
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.EventId).IsRequired();
            b.Property(x => x.EventDayId).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Type).HasMaxLength(32).IsRequired();
            b.Property(x => x.StartTime).HasColumnType("time without time zone");
            b.Property(x => x.EndTime).HasColumnType("time without time zone");
            b.Property(x => x.LocationName).HasMaxLength(200);
            b.Property(x => x.Address).HasMaxLength(500);
            b.Property(x => x.Directions).HasMaxLength(2000);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.CheckInEnabled).IsRequired().HasDefaultValue(false);
            b.Property(x => x.CheckInMode).HasMaxLength(32).IsRequired().HasDefaultValue("EntryOnly");
            b.Property(x => x.MenuText).HasMaxLength(2000);
            b.Property(x => x.SurveyUrl).HasMaxLength(500);

            b.HasIndex(x => new { x.OrganizationId, x.EventDayId, x.StartTime });
            b.HasIndex(x => new { x.OrganizationId, x.EventId });

            b.HasOne(x => x.Organization)
                .WithMany(x => x.EventActivities)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
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
                table.HasCheckConstraint(
                    "CK_participant_details_arrival_baggage_pieces",
                    "\"ArrivalBaggagePieces\" IS NULL OR \"ArrivalBaggagePieces\" > 0");
                table.HasCheckConstraint(
                    "CK_participant_details_arrival_baggage_total_kg",
                    "\"ArrivalBaggageTotalKg\" IS NULL OR \"ArrivalBaggageTotalKg\" > 0");
                table.HasCheckConstraint(
                    "CK_participant_details_return_baggage_pieces",
                    "\"ReturnBaggagePieces\" IS NULL OR \"ReturnBaggagePieces\" > 0");
                table.HasCheckConstraint(
                    "CK_participant_details_return_baggage_total_kg",
                    "\"ReturnBaggageTotalKg\" IS NULL OR \"ReturnBaggageTotalKg\" > 0");
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
            b.Property(x => x.ArrivalBaggagePieces);
            b.Property(x => x.ArrivalBaggageTotalKg);

            b.Property(x => x.ReturnAirline).HasMaxLength(100);
            b.Property(x => x.ReturnDepartureAirport).HasMaxLength(100);
            b.Property(x => x.ReturnArrivalAirport).HasMaxLength(100);
            b.Property(x => x.ReturnFlightCode).HasMaxLength(100);
            b.Property(x => x.ReturnDepartureTime).HasColumnType("time without time zone");
            b.Property(x => x.ReturnArrivalTime).HasColumnType("time without time zone");
            b.Property(x => x.ReturnPnr).HasMaxLength(100);
            b.Property(x => x.ReturnBaggageAllowance).HasMaxLength(100);
            b.Property(x => x.ReturnBaggagePieces);
            b.Property(x => x.ReturnBaggageTotalKg);

        });

        modelBuilder.Entity<PortalSessionEntity>(b =>
        {
            b.ToTable("portal_sessions");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.ParticipantId).IsRequired();
            b.Property(x => x.EventId).IsRequired();
            b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            b.Property(x => x.ExpiresAt).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.LastSeenAt).IsRequired();

            b.HasIndex(x => x.OrganizationId);
            b.HasIndex(x => x.ParticipantId);
            b.HasIndex(x => new { x.EventId, x.ParticipantId });
            b.HasIndex(x => x.ExpiresAt);
            b.HasIndex(x => x.LastSeenAt);
            b.HasIndex(x => x.TokenHash).IsUnique();

            b.HasOne(x => x.Organization)
                .WithMany(x => x.PortalSessions)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Participant)
                .WithMany()
                .HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
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
