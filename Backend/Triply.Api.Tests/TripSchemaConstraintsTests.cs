// Backend/Triply.Api.Tests/TripSchemaConstraintsTests.cs
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Entities;
using Xunit;

namespace Triply.Api.Tests;

// Task 4 AC: FK/NOT NULL/CHECK rules exact per §6, §8, §18; place_id non-nullable (FR-AI-002)
public class TripSchemaConstraintsTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public TripSchemaConstraintsTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TriplyDb_TripSchemaTests;Trusted_Connection=True;")
            .Options;

        _db = new ApplicationDbContext(options);
        _db.Database.EnsureDeleted();
        _db.Database.Migrate();
    }

    private (Guid userId, long placeId) SeedUserAndPlace()
    {
        var user = new ApplicationUser { UserName = "trip-test@triply.dev", Email = "trip-test@triply.dev" };
        _db.Users.Add(user);

        var country = new Country { Name = "TestCountry", IsoCode = "TC" };
        _db.Countries.Add(country);
        _db.SaveChanges();

        var destination = new Destination { CountryId = country.Id, Name = "TestCity" };
        _db.Destinations.Add(destination);

        var currency = new Currency { IsoCode = "TST", Symbol = "T" };
        _db.Currencies.Add(currency);
        _db.SaveChanges();

        var place = new Place
        {
            DestinationId = destination.Id,
            PlaceCategoryId = _db.PlaceCategories.First().Id,
            Name = "TestPlace",
            ReferencePrice = 10,
            CurrencyId = currency.Id,
            CostCategoryId = _db.CostCategories.First().Id
        };
        _db.Places.Add(place);
        _db.SaveChanges();

        return (user.Id, place.Id);
    }

    // FR-AI-002 DB-level guarantee: an ItineraryItem cannot be saved without a real Place
    [Fact]
    public void ItineraryItem_PlaceId_IsNonNullableAtDbLevel()
    {
        var placeType = typeof(ItineraryItem).GetProperty(nameof(ItineraryItem.PlaceId))!.PropertyType;
        Assert.Equal(typeof(long), placeType); // not long?, i.e. never nullable in the CLR model

        var fk = _db.Model.FindEntityType(typeof(ItineraryItem))!
            .FindNavigation(nameof(ItineraryItem.Place))!
            .ForeignKey;
        Assert.False(fk.IsRequired == false); // required = true
    }

    [Fact]
    public void Trip_TravelerCount_Zero_ViolatesCheckConstraint()
    {
        var (userId, _) = SeedUserAndPlace();
        _db.Trips.Add(new Trip
        {
            UserId = userId,
            PlanningMode = "DESTINATION_FIRST",
            Status = "DRAFT",
            TravelerCount = 0 // invalid, CHECK > 0
        });
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void Trip_InvalidStatus_ViolatesCheckConstraint()
    {
        var (userId, _) = SeedUserAndPlace();
        _db.Trips.Add(new Trip
        {
            UserId = userId,
            PlanningMode = "DESTINATION_FIRST",
            Status = "NOT_A_REAL_STATUS", // invalid enum-like value
            TravelerCount = 1
        });
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void CostEstimate_NegativeAmount_ViolatesCheckConstraint()
    {
        var (userId, _) = SeedUserAndPlace();
        var trip = new Trip { UserId = userId, PlanningMode = "DESTINATION_FIRST", Status = "DRAFT", TravelerCount = 1 };
        _db.Trips.Add(trip);
        _db.SaveChanges();

        _db.CostEstimates.Add(new CostEstimate
        {
            TripId = trip.Id,
            CostCategoryId = _db.CostCategories.First().Id,
            Amount = -5, // invalid, CHECK >= 0
            CurrencyId = _db.Currencies.First().Id
        });
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void IX_Trip_UserId_Exists()
    {
        var index = _db.Model.FindEntityType(typeof(Trip))!
            .GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Trip.UserId)));
        Assert.NotNull(index);
    }

    [Fact]
    public void IX_ItineraryItem_PlaceId_Exists()
    {
        var index = _db.Model.FindEntityType(typeof(ItineraryItem))!
            .GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(ItineraryItem.PlaceId)));
        Assert.NotNull(index);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}