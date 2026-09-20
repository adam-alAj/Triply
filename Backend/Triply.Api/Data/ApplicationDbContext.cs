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
    public DbSet<PlaceInterest> PlaceInterests => Set<PlaceInterest>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripInterest> TripInterests => Set<TripInterest>();
    public DbSet<Itinerary> Itineraries => Set<Itinerary>();
    public DbSet<ItineraryDay> ItineraryDays => Set<ItineraryDay>();
    public DbSet<ItineraryItem> ItineraryItems => Set<ItineraryItem>();
    public DbSet<CostEstimate> CostEstimates => Set<CostEstimate>();
    public DbSet<AIGeneration> AIGenerations => Set<AIGeneration>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

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
        b.Entity<Place>()
            .Property(x => x.Name)
            .HasColumnType("varchar(200)");
        b.Entity<Place>()
            .HasIndex(x => new { x.DestinationId, x.Name })
            .IsUnique();

        // ---- PlaceInterest (Place ↔ InterestCategory, §6.5 + interest-aware suggestions) ----
      b.Entity<PlaceInterest>().HasKey(x => new
{
    x.PlaceId,
    x.InterestCategoryId
});

b.Entity<PlaceInterest>()
    .HasOne(x => x.Place)
    .WithMany(x => x.PlaceInterests)
    .HasForeignKey(x => x.PlaceId)
    .OnDelete(DeleteBehavior.Cascade);

b.Entity<PlaceInterest>()
    .HasOne(x => x.InterestCategory)
    .WithMany(x => x.PlaceInterests)
    .HasForeignKey(x => x.InterestCategoryId)
    .OnDelete(DeleteBehavior.Restrict);

        // ---- UserPreferences ----
        b.Entity<UserPreferences>().HasKey(x => x.UserId);
        b.Entity<UserPreferences>()
            .HasOne(x => x.User).WithOne()
            .HasForeignKey<UserPreferences>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserPreferences>()
            .HasOne(x => x.PreferredCurrency).WithMany()
            .HasForeignKey(x => x.PreferredCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserPreferences>().Property(x => x.DistanceUnit).HasMaxLength(10);
        b.Entity<UserPreferences>().Property(x => x.Pacing).HasMaxLength(20);

        // ---- Trip (§6.9, §8) ----
        b.Entity<Trip>().HasIndex(x => x.UserId); // IX_Trip_UserId
        b.Entity<Trip>().HasIndex(x => x.Status); // IX_Trip_Status
        b.Entity<Trip>()
            .Property(x => x.Version)
            .IsConcurrencyToken();

        b.Entity<Trip>()
            .HasOne(x => x.Destination).WithMany()
            .HasForeignKey(x => x.DestinationId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Trip>().Property(x => x.PlanningMode).HasMaxLength(20);
        b.Entity<Trip>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<Trip>().Property(x => x.Title).HasMaxLength(200);
        b.Entity<Trip>().Property(x => x.CoverImageUrl).HasMaxLength(1000);
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

        // Coordinates are more precise than monetary decimal fields.
        b.Entity<Destination>()
            .Property(x => x.Latitude)
            .HasColumnType("decimal(9,6)");
        b.Entity<Destination>()
            .Property(x => x.Longitude)
            .HasColumnType("decimal(9,6)");

        // email UNIQUE enforced at DB level, not just app-level RequireUniqueEmail
b.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        // ---- CHECK constraints (§16 Data Integrity Rules) ----
        b.Entity<Trip>().ToTable(t => t.HasCheckConstraint(
            "CK_Trip_TravelerCount", "[TravelerCount] > 0"));
        b.Entity<Trip>().ToTable(t => t.HasCheckConstraint(
            "CK_Trip_PlanningMode",
            "[PlanningMode] IN ('DESTINATION_FIRST','BUDGET_FIRST')"));
        b.Entity<Trip>().ToTable(t => t.HasCheckConstraint(
            "CK_Trip_Status",
            "[Status] IN ('DRAFT','GENERATING','GENERATED','MODIFIED','SAVED','ARCHIVED')"));

        b.Entity<ItineraryDay>().ToTable(t => t.HasCheckConstraint(
            "CK_ItineraryDay_DayNumber", "[DayNumber] > 0"));

        b.Entity<ItineraryItem>().ToTable(t => t.HasCheckConstraint(
            "CK_ItineraryItem_TimeSlot",
            "[TimeSlot] IN ('MORNING','AFTERNOON','EVENING')"));

        b.Entity<CostEstimate>().ToTable(t => t.HasCheckConstraint(
            "CK_CostEstimate_Amount", "[Amount] >= 0"));

        b.Entity<AIGeneration>().ToTable(t => t.HasCheckConstraint(
            "CK_AIGeneration_Status",
            "[Status] IN ('PENDING','SUCCEEDED','FAILED_VALIDATION','FAILED_ERROR')"));
        // ---- Stable reference data ----
        // These are shared lookup values used by the backend/tests.
        // Destination/Place/PlaceInterest data is NOT seeded here; it comes
        // from AI/01-Dataset/curated-data through the dataset seeder.

        b.Entity<Country>().HasData(
            new Country { Id = 1, Name = "France", IsoCode = "FR" },
            new Country { Id = 2, Name = "Jordan", IsoCode = "JO" },
            new Country { Id = 3, Name = "United States", IsoCode = "US" });

        b.Entity<Currency>().HasData(
            new Currency { Id = 1, IsoCode = "EUR", Symbol = "€" },
            new Currency { Id = 2, IsoCode = "JOD", Symbol = "د.أ" },
            new Currency { Id = 3, IsoCode = "USD", Symbol = "$" });

        b.Entity<InterestCategory>().HasData(
            new InterestCategory { Id = 1, Code = "NATURE", Label = "Nature" },
            new InterestCategory { Id = 2, Code = "HISTORY", Label = "History" },
            new InterestCategory { Id = 3, Code = "FOOD", Label = "Food" },
            new InterestCategory { Id = 4, Code = "SHOPPING", Label = "Shopping" },
            new InterestCategory { Id = 5, Code = "ADVENTURE", Label = "Adventure" },
            new InterestCategory { Id = 6, Code = "CULTURE", Label = "Culture" },
            new InterestCategory { Id = 7, Code = "RELAXATION", Label = "Relaxation" },
            new InterestCategory { Id = 8, Code = "OTHER", Label = "Other" });

        b.Entity<CostCategory>().HasData(
            new CostCategory { Id = 1, Code = "ACCOMMODATION", Label = "Accommodation" },
            new CostCategory { Id = 2, Code = "TRANSPORTATION", Label = "Transportation" },
            new CostCategory { Id = 3, Code = "FOOD", Label = "Food" },
            new CostCategory { Id = 4, Code = "ACTIVITIES", Label = "Activities" },
            new CostCategory { Id = 5, Code = "OTHER", Label = "Other" });

        b.Entity<PlaceCategory>().HasData(
            new PlaceCategory { Id = 1, Code = "ATTRACTION", Label = "Attraction" },
            new PlaceCategory { Id = 2, Code = "RESTAURANT", Label = "Restaurant" },
            new PlaceCategory { Id = 3, Code = "ACTIVITY", Label = "Activity" },
            new PlaceCategory { Id = 4, Code = "ACCOMMODATION", Label = "Accommodation" },
            new PlaceCategory { Id = 5, Code = "TRANSPORT", Label = "Transport" });

        // ---- Curated dataset ownership ----
        // Destinations, Places, and PlaceInterests are seeded
        // by AI/01-Dataset/seed/seed_places.py from the versioned curated CSVs.
        // Do NOT add Destination/Place HasData here: the AI curated dataset is
        // the runtime source of truth for destination/place data.
        // Extra_AI_Context.csv is prompt context only and is intentionally not
        // mapped to a database table.
    }
}
