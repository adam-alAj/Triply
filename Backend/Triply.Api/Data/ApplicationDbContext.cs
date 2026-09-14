using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Entities;
using Microsoft.AspNetCore.Identity;

namespace Triply.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<InterestCategory> InterestCategories => Set<InterestCategory>();
    public DbSet<CostCategory> CostCategories => Set<CostCategory>();
    public DbSet<PlaceCategory> PlaceCategories => Set<PlaceCategory>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripInterest> TripInterests => Set<TripInterest>();
    public DbSet<Itinerary> Itineraries => Set<Itinerary>();
    public DbSet<ItineraryDay> ItineraryDays => Set<ItineraryDay>();
    public DbSet<ItineraryItem> ItineraryItems => Set<ItineraryItem>();
    public DbSet<CostEstimate> CostEstimates => Set<CostEstimate>();
    public DbSet<AIGeneration> AIGenerations => Set<AIGeneration>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ---- Reference tables: UNIQUE constraints (Task 2, §16) ----
        b.Entity<Country>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Country>().HasIndex(x => x.IsoCode).IsUnique();
        b.Entity<Currency>().HasIndex(x => x.IsoCode).IsUnique();
        b.Entity<InterestCategory>().HasIndex(x => x.Code).IsUnique();
        b.Entity<CostCategory>().HasIndex(x => x.Code).IsUnique();
        b.Entity<PlaceCategory>().HasIndex(x => x.Code).IsUnique();

        // ---- Destination / Place ----
        b.Entity<Destination>()
            .HasIndex(x => new { x.CountryId, x.Name }).IsUnique();
        b.Entity<Place>()
            .HasOne(x => x.Destination).WithMany(x => x.Places)
            .HasForeignKey(x => x.DestinationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Place>().HasIndex(x => x.DestinationId); // IX_Place_DestinationId

        // ---- Trip (§6.9, §8) ----
        b.Entity<Trip>().HasIndex(x => x.UserId); // IX_Trip_UserId
        b.Entity<Trip>().HasIndex(x => x.Status); // IX_Trip_Status
        b.Entity<Trip>()
            .HasOne(x => x.Destination).WithMany()
            .HasForeignKey(x => x.DestinationId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Trip>().Property(x => x.PlanningMode).HasMaxLength(20);
        b.Entity<Trip>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<Trip>().HasQueryFilter(x => x.DeletedAt == null); // soft-delete safeguard, §28 risk mitigation

        // ---- TripInterest (composite PK, §6.10) ----
        b.Entity<TripInterest>().HasKey(x => new { x.TripId, x.InterestCategoryId });
        b.Entity<TripInterest>()
            .HasOne(x => x.Trip).WithMany(x => x.TripInterests)
            .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TripInterest>()
            .HasOne(x => x.InterestCategory).WithMany()
            .HasForeignKey(x => x.InterestCategoryId).OnDelete(DeleteBehavior.Restrict);

        // ---- Itinerary (1:1 with Trip) ----
        b.Entity<Itinerary>().HasIndex(x => x.TripId).IsUnique();
        b.Entity<Itinerary>()
            .HasOne(x => x.Trip).WithOne(x => x.Itinerary)
            .HasForeignKey<Itinerary>(x => x.TripId).OnDelete(DeleteBehavior.Cascade);

        // ---- ItineraryDay ----
        b.Entity<ItineraryDay>().HasIndex(x => x.ItineraryId); // IX_ItineraryDay_ItineraryId
        b.Entity<ItineraryDay>().HasIndex(x => new { x.ItineraryId, x.DayNumber }).IsUnique();
        b.Entity<ItineraryDay>()
            .HasOne(x => x.Itinerary).WithMany(x => x.Days)
            .HasForeignKey(x => x.ItineraryId).OnDelete(DeleteBehavior.Cascade);

        // ---- ItineraryItem — FR-AI-002 DB guarantee: Place FK is NOT NULL/RESTRICT ----
        b.Entity<ItineraryItem>().HasIndex(x => x.ItineraryDayId); // IX_ItineraryItem_DayId
        b.Entity<ItineraryItem>().HasIndex(x => x.PlaceId);        // IX_ItineraryItem_PlaceId
        b.Entity<ItineraryItem>()
            .HasOne(x => x.ItineraryDay).WithMany(x => x.Items)
            .HasForeignKey(x => x.ItineraryDayId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ItineraryItem>()
            .HasOne(x => x.Place).WithMany()
            .HasForeignKey(x => x.PlaceId).IsRequired().OnDelete(DeleteBehavior.Restrict);
        b.Entity<ItineraryItem>().Property(x => x.TimeSlot).HasMaxLength(20);

        // ---- CostEstimate ----
        b.Entity<CostEstimate>().HasIndex(x => x.TripId); // IX_CostEstimate_TripId
        b.Entity<CostEstimate>().HasIndex(x => new { x.TripId, x.CostCategoryId }).IsUnique();
        b.Entity<CostEstimate>()
            .HasOne(x => x.Trip).WithMany(x => x.CostEstimates)
            .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);

        // ---- AIGeneration ----
        b.Entity<AIGeneration>().HasIndex(x => x.TripId); // IX_AIGeneration_TripId
        b.Entity<AIGeneration>()
            .HasOne(x => x.Trip).WithMany(x => x.AIGenerations)
            .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);

        // ---- Decimal precision (avoid silent truncation warnings) ----
        foreach (var prop in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetColumnType("decimal(10,2)");
        }
    }
}
