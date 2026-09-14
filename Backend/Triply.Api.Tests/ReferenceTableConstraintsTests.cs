// Backend/Triply.Api.Tests/ReferenceTableConstraintsTests.cs
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Entities;
using Xunit;

namespace Triply.Api.Tests;

// Task 2 AC: "unique constraints on code/name/iso_code enforced and tested."
// Uses a real SQL Server test database (LocalDB) because EF Core's InMemory
// provider does NOT enforce unique indexes — matches the team's demonstrated
// testing pattern (xUnit against LocalDB), not a fake/in-memory shortcut.
public class ReferenceTableConstraintsTests : IDisposable
{
    private readonly ApplicationDbContext _db;

    public ReferenceTableConstraintsTests()
{
    var connectionString =
        Environment.GetEnvironmentVariable("TRIPLY_TEST_DB_CONNECTION")
        ?? "Server=(localdb)\\mssqllocaldb;Database=TriplyDb_ConstraintTests;Trusted_Connection=True;TrustServerCertificate=True;";

    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(connectionString)
        .Options;

    _db = new ApplicationDbContext(options);
    _db.Database.EnsureDeleted(); // clean slate per test class run
    _db.Database.Migrate();       // applies schema + HasData seed
}

    [Fact]
    public void InterestCategory_DuplicateCode_ThrowsOnSave()
    {
        _db.InterestCategories.Add(new InterestCategory { Code = "NATURE", Label = "Duplicate" });
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void CostCategory_DuplicateCode_ThrowsOnSave()
    {
        _db.CostCategories.Add(new CostCategory { Code = "FOOD", Label = "Duplicate" });
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void PlaceCategory_DuplicateCode_ThrowsOnSave()
    {
        _db.PlaceCategories.Add(new PlaceCategory { Code = "ATTRACTION", Label = "Duplicate" });
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void Country_DuplicateName_ThrowsOnSave()
    {
        _db.Countries.Add(new Country { Name = "TestLand", IsoCode = "TL" });
        _db.SaveChanges();

        _db.Countries.Add(new Country { Name = "TestLand", IsoCode = "TM" }); // same Name, different IsoCode
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void Country_DuplicateIsoCode_ThrowsOnSave()
    {
        _db.Countries.Add(new Country { Name = "CountryA", IsoCode = "XX" });
        _db.SaveChanges();

        _db.Countries.Add(new Country { Name = "CountryB", IsoCode = "XX" }); // same IsoCode
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    [Fact]
    public void Currency_DuplicateIsoCode_ThrowsOnSave()
    {
        _db.Currencies.Add(new Currency { IsoCode = "ZZZ", Symbol = "Z" });
        _db.SaveChanges();

        _db.Currencies.Add(new Currency { IsoCode = "ZZZ", Symbol = "Z2" });
        Assert.Throws<DbUpdateException>(() => _db.SaveChanges());
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}