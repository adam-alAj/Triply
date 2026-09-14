using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Triply.Api.Data;
using Triply.Api.Entities;
using Xunit;

namespace Triply.Api.Tests;

public class UserIdentityTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserIdentityTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TriplyDb_UserTests;Trusted_Connection=True;")
            .Options;

        _db = new ApplicationDbContext(options);
        _db.Database.EnsureDeleted();
        _db.Database.Migrate();

        var store = new UserStore<ApplicationUser, IdentityRole<Guid>, ApplicationDbContext, Guid>(_db);
        _userManager = new UserManager<ApplicationUser>(
            store, null, new PasswordHasher<ApplicationUser>(), null, null, null, null, null, null);
    }

    [Fact]
    public async Task CreateUser_PasswordHash_IsNeverPlaintext()
    {
        var user = new ApplicationUser { UserName = "test@triply.dev", Email = "test@triply.dev" };
        var result = await _userManager.CreateAsync(user, "P@ssw0rd123");

        Assert.True(result.Succeeded);
        Assert.NotEqual("P@ssw0rd123", user.PasswordHash);
        Assert.StartsWith("A", user.PasswordHash); // ASP.NET Core Identity v3 hash format prefix
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_ThrowsOnSave()
    {
        var user1 = new ApplicationUser { UserName = "dup@triply.dev", Email = "dup@triply.dev" };
        await _userManager.CreateAsync(user1, "P@ssw0rd123");

        var user2 = new ApplicationUser { UserName = "dup2@triply.dev", Email = "dup@triply.dev" };
        var user2Entity = new ApplicationUser
        {
            UserName = "dup2@triply.dev",
            Email = "dup@triply.dev",
            NormalizedEmail = "DUP@TRIPLY.DEV"
        };
        _db.Users.Add(user2Entity);
        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}