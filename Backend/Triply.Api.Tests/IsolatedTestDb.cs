using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Triply.Api.Tests;

/// <summary>
/// Gives a test factory its own database even when TRIPLY_TEST_DB_CONNECTION points
/// at a shared CI server (most factories share that database). Required by tests
/// that must manipulate constraints (unique index) or seed the full curated
/// reference dataset without affecting concurrently running test classes.
///
/// Both configuration channels used by CustomWebApplicationFactory are overridden
/// (UseSetting + a configuration source registered AFTER the base one, so it wins).
/// </summary>
internal static class IsolatedTestDb
{
    public static void Configure(IWebHostBuilder builder, string purpose)
    {
        var database = $"TriplyDb_{purpose}_{Guid.NewGuid():N}";
        var baseConnection = Environment.GetEnvironmentVariable("TRIPLY_TEST_DB_CONNECTION");

        var connectionString = !string.IsNullOrWhiteSpace(baseConnection)
            ? ReplaceDatabase(baseConnection, database)
            : $"Server=(localdb)\\mssqllocaldb;Database={database};Trusted_Connection=True;TrustServerCertificate=True";

        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString
            }));
    }

    private static string ReplaceDatabase(string connectionString, string database)
    {
        var parts = connectionString.Split(';');
        for (var i = 0; i < parts.Length; i++)
        {
            var separatorIndex = parts[i].IndexOf('=');
            if (separatorIndex > 0 &&
                parts[i].Substring(0, separatorIndex).Trim()
                    .Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = "Database=" + database;
            }
        }

        return string.Join(";", parts);
    }
}
