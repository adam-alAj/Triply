// Backend/Triply.Api.Tests/CustomWebApplicationFactory.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Triply.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] =
                    "Server=(localdb)\\mssqllocaldb;Database=TriplyDb_AuthIntegrationTests;Trusted_Connection=True;",
                ["Jwt:Key"] = "Integration-Test-Only-Signing-Key-Not-For-Production-12345",
                ["Jwt:Issuer"] = "Triply",
                ["Jwt:Audience"] = "TriplyClients"
            });
        });
    }
}