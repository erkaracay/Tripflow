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
    public DbSet<EventDocTabEntity> EventDocTabs => Set<EventDocTabEntity>();
    public DbSet<PortalSessionEntity> PortalSessions => Set<PortalSessionEntity>();
    public DbSet<EventPortalEntity> EventPortals => Set<EventPortalEntity>();
    public DbSet<CheckInEntity> CheckIns => Set<CheckInEntity>();
    public DbSet<EventParticipantLogEntity> EventParticipantLogs => Set<EventParticipantLogEntity>();
    public DbSet<ActivityParticipantLogEntity> ActivityParticipantLogs => Set<ActivityParticipantLogEntity>();
    public DbSet<EventItemEntity> EventItems => Set<EventItemEntity>();
    public DbSet<ParticipantItemLogEntity> ParticipantItemLogs => Set<ParticipantItemLogEntity>();
    public DbSet<EventGuideEntity> EventGuides => Set<EventGuideEntity>();
    public DbSet<ParticipantActivityWillNotAttendEntity> ParticipantActivityWillNotAttend => Set<ParticipantActivityWillNotAttendEntity>();

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
            b.Property(x => x.LogoUrl).HasMaxLength(500);
            b.Property(x => x.EventAccessCode).HasMaxLength(16).IsRequired();
            b.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => new { x.OrganizationId, x.StartDate });
            b.HasIndex(x => x.EventAccessCode).IsUnique();

            b.HasOne(x => x.Organization)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.EventGuides)
                .WithOne(x => x.Event)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

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
            b.Property(x => x.Notes).HasMaxLength(15000);
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
            b.ToTable("event_activities");
            // No time-range constraint: allow end < start for activities over midnight (e.g. 23:00–00:15)
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
            b.Property(x => x.Directions).HasMaxLength(15000);
            b.Property(x => x.Notes).HasMaxLength(15000);
            b.Property(x => x.CheckInEnabled).IsRequired().HasDefaultValue(false);
            b.Property(x => x.RequiresCheckIn).IsRequired().HasDefaultValue(false);
            b.Property(x => x.CheckInMode).HasMaxLength(32).IsRequired().HasDefaultValue("EntryOnly");
            b.Property(x => x.MenuText).HasMaxLength(15000);
            b.Property(x => x.ProgramContent).HasMaxLength(15000);
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

            b.Property(x => x.WillNotAttend).HasDefaultValue(false).IsRequired();

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
            b.Property(x => x.BoardType).HasMaxLength(50);
            b.Property(x => x.PersonNo).HasMaxLength(50);
            b.Property(x => x.AgencyName).HasMaxLength(200);
            b.Property(x => x.City).HasMaxLength(100);
            b.Property(x => x.FlightCity).HasMaxLength(100);

            b.Property(x => x.HotelCheckInDate).HasColumnType("date");
            b.Property(x => x.HotelCheckOutDate).HasColumnType("date");

            b.Property(x => x.TicketNo).HasMaxLength(100);
            b.Property(x => x.ArrivalTicketNo).HasMaxLength(100);
            b.Property(x => x.ReturnTicketNo).HasMaxLength(100);
            b.Property(x => x.AttendanceStatus).HasMaxLength(100);

            b.Property(x => x.InsuranceCompanyName).HasMaxLength(200);
            b.Property(x => x.InsurancePolicyNo).HasMaxLength(100);
            b.Property(x => x.InsuranceStartDate).HasColumnType("date");
            b.Property(x => x.InsuranceEndDate).HasColumnType("date");

            b.Property(x => x.ArrivalAirline).HasMaxLength(100);
            b.Property(x => x.ArrivalDepartureAirport).HasMaxLength(100);
            b.Property(x => x.ArrivalArrivalAirport).HasMaxLength(100);
            b.Property(x => x.ArrivalFlightCode).HasMaxLength(100);
            b.Property(x => x.ArrivalFlightDate).HasColumnType("date");
            b.Property(x => x.ArrivalDepartureTime).HasColumnType("time without time zone");
            b.Property(x => x.ArrivalArrivalTime).HasColumnType("time without time zone");
            b.Property(x => x.ArrivalPnr).HasMaxLength(100);
            b.Property(x => x.ArrivalBaggageAllowance).HasMaxLength(100);
            b.Property(x => x.ArrivalBaggagePieces);
            b.Property(x => x.ArrivalBaggageTotalKg);
            b.Property(x => x.ArrivalCabinBaggage).HasMaxLength(100);

            b.Property(x => x.ReturnAirline).HasMaxLength(100);
            b.Property(x => x.ReturnDepartureAirport).HasMaxLength(100);
            b.Property(x => x.ReturnArrivalAirport).HasMaxLength(100);
            b.Property(x => x.ReturnFlightCode).HasMaxLength(100);
            b.Property(x => x.ReturnFlightDate).HasColumnType("date");
            b.Property(x => x.ReturnDepartureTime).HasColumnType("time without time zone");
            b.Property(x => x.ReturnArrivalTime).HasColumnType("time without time zone");
            b.Property(x => x.ReturnPnr).HasMaxLength(100);
            b.Property(x => x.ReturnBaggageAllowance).HasMaxLength(100);
            b.Property(x => x.ReturnBaggagePieces);
            b.Property(x => x.ReturnBaggageTotalKg);
            b.Property(x => x.ReturnCabinBaggage).HasMaxLength(100);

        });

        modelBuilder.Entity<EventDocTabEntity>(b =>
        {
            b.ToTable("event_doc_tabs");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.EventId).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Type).HasMaxLength(50).IsRequired();
            b.Property(x => x.SortOrder).HasDefaultValue(1);
            b.Property(x => x.IsActive).HasDefaultValue(true);
            b.Property(x => x.ContentJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();

            b.HasIndex(x => new { x.OrganizationId, x.EventId, x.SortOrder });

            b.HasOne(x => x.Organization)
                .WithMany(x => x.DocTabs)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Event)
                .WithMany(x => x.DocTabs)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<EventParticipantLogEntity>(b =>
        {
            b.ToTable("event_participant_logs");
            b.HasKey(x => x.Id);

            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.EventId).IsRequired();
            b.Property(x => x.ParticipantId);

            b.Property(x => x.Direction)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();

            b.Property(x => x.Method)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();

            b.Property(x => x.Result).HasMaxLength(32).IsRequired().HasDefaultValue("Success");

            b.Property(x => x.IpAddress);
            b.Property(x => x.UserAgent);

            b.Property(x => x.ActorUserId);
            b.Property(x => x.ActorRole).HasMaxLength(32);
            b.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");

            b.HasIndex(x => new { x.OrganizationId, x.EventId, x.CreatedAt })
                .IsDescending(false, false, true);
            b.HasIndex(x => new { x.OrganizationId, x.ParticipantId, x.CreatedAt })
                .IsDescending(false, false, true);
            b.HasIndex(x => new { x.OrganizationId, x.EventId, x.ParticipantId, x.CreatedAt })
                .IsDescending(false, false, false, true);

            b.HasOne(x => x.Organization)
                .WithMany()
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivityParticipantLogEntity>(b =>
        {
            b.ToTable("activity_participant_logs");
            b.HasKey(x => x.Id);
            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.EventId).IsRequired();
            b.Property(x => x.ActivityId).IsRequired();
            b.Property(x => x.ParticipantId);
            b.Property(x => x.Direction).HasMaxLength(16).IsRequired();
            b.Property(x => x.Method).HasMaxLength(16).IsRequired();
            b.Property(x => x.Result).HasMaxLength(32).IsRequired();
            b.Property(x => x.ActorUserId);
            b.Property(x => x.ActorRole).HasMaxLength(32);
            b.Property(x => x.IpAddress);
            b.Property(x => x.UserAgent);
            b.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");
            b.HasIndex(x => new { x.OrganizationId, x.EventId, x.ActivityId, x.CreatedAt }).IsDescending(false, false, false, true);
            b.HasIndex(x => new { x.OrganizationId, x.ActivityId, x.ParticipantId, x.CreatedAt }).IsDescending(false, false, false, true);
        });

        modelBuilder.Entity<EventItemEntity>(b =>
        {
            b.ToTable("event_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.EventId).IsRequired();
            b.Property(x => x.Type).HasMaxLength(32).IsRequired();
            b.Property(x => x.Title).HasMaxLength(100).IsRequired();
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
            b.Property(x => x.SortOrder).IsRequired().HasDefaultValue(1);
            b.HasIndex(x => new { x.OrganizationId, x.EventId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<ParticipantItemLogEntity>(b =>
        {
            b.ToTable("participant_item_logs");
            b.HasKey(x => x.Id);
            b.Property(x => x.OrganizationId).IsRequired();
            b.Property(x => x.EventId).IsRequired();
            b.Property(x => x.ItemId).IsRequired();
            b.Property(x => x.ParticipantId);
            b.Property(x => x.Action).HasMaxLength(16).IsRequired();
            b.Property(x => x.Method).HasMaxLength(16).IsRequired();
            b.Property(x => x.Result).HasMaxLength(32).IsRequired();
            b.Property(x => x.ActorUserId);
            b.Property(x => x.ActorRole).HasMaxLength(32);
            b.Property(x => x.IpAddress);
            b.Property(x => x.UserAgent);
            b.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");
            b.HasIndex(x => new { x.OrganizationId, x.EventId, x.ItemId, x.CreatedAt }).IsDescending(false, false, false, true);
            b.HasIndex(x => new { x.OrganizationId, x.ItemId, x.ParticipantId, x.CreatedAt }).IsDescending(false, false, false, true);
        });

        modelBuilder.Entity<EventGuideEntity>(b =>
        {
            b.ToTable("event_guides");
            b.HasKey(x => new { x.EventId, x.GuideUserId });

            b.Property(x => x.EventId).IsRequired();
            b.Property(x => x.GuideUserId).IsRequired();

            b.HasOne(x => x.Event)
                .WithMany(x => x.EventGuides)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.GuideUser)
                .WithMany()
                .HasForeignKey(x => x.GuideUserId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => x.GuideUserId);
            b.HasIndex(x => x.EventId);
        });

        modelBuilder.Entity<ParticipantActivityWillNotAttendEntity>(b =>
        {
            b.ToTable("participant_activity_will_not_attend");
            b.HasKey(x => x.Id);

            b.Property(x => x.ParticipantId).IsRequired();
            b.Property(x => x.ActivityId).IsRequired();
            b.Property(x => x.WillNotAttend).IsRequired().HasDefaultValue(false);
            b.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");
            b.Property(x => x.UpdatedAt);

            b.HasIndex(x => new { x.ParticipantId, x.ActivityId }).IsUnique();

            b.HasOne(x => x.Participant)
                .WithMany()
                .HasForeignKey(x => x.ParticipantId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Activity)
                .WithMany()
                .HasForeignKey(x => x.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
