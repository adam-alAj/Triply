using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Triply.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName =
        $"TriplyDb_Tests_{Guid.NewGuid():N}";

    private const string TestJwtKey =
        "Integration-Test-Only-Signing-Key-Not-For-Production-12345";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        var connectionString =
            Environment.GetEnvironmentVariable("TRIPLY_TEST_DB_CONNECTION")
            ?? $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True;";

        // Force the test configuration values into the application
        // configuration used by Program.cs.
        builder.UseSetting(
            "ConnectionStrings:Default",
            connectionString);

        builder.UseSetting("Jwt:Key", TestJwtKey);
        builder.UseSetting("Jwt:Issuer", "Triply");
        builder.UseSetting("Jwt:Audience", "TriplyClients");
        builder.UseSetting("Jwt:ExpiresMinutes", "60");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] =
                    connectionString,

                ["Jwt:Key"] = TestJwtKey,

                ["Jwt:Issuer"] = "Triply",

                ["Jwt:Audience"] = "TriplyClients",

                ["Jwt:ExpiresMinutes"] = "60"
            });
        });
    }
}