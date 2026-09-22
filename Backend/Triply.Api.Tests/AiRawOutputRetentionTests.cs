using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Trip.Dtos;

namespace Triply.Api.Tests;

/// <summary>
/// Gap 4 regression tests for the documented 30-day AI raw-output retention policy
/// (Docs/05 §16, audit G-06): expired <c>AIGeneration.RawOutput</c> payloads are
/// nulled while attempt rows, statuses, validation errors, snapshots and
/// timestamps are preserved; recent payloads are untouched; a second pass is a
/// no-op; a disabled window (&lt;= 0 days) purges nothing.
/// </summary>
public sealed class AiRawOutputRetentionTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AiRawOutputRetentionTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Purge_ExpiredRawOutput_IsNulled_PreservingRowsAuditColumns_AndRecentPayloads()
    {
        // --- Arrange: user + trip + three generation attempts ---
        var email = $"retention_{Guid.NewGuid():N}@triply.dev";
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123", "Retention Test"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(auth?.Token));
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.Token);

        long parisId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            parisId = await db.Destinations
                .Where(d => d.Name == "Paris")
                .Select(d => d.Id)
                .FirstAsync();
        }

        var createResponse = await _client.PostAsJsonAsync("/api/trips", new
        {
            planningMode = "DESTINATION_FIRST",
            destinationId = parisId,
            startDate = "2026-11-01",
            endDate = "2026-11-03",
            travelerCount = 1
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, createResponse.StatusCode);
        var trip = await createResponse.Content.ReadFromJsonAsync<TripResponse>();
        Assert.NotNull(trip);

        var expiredRaw = "{\"model\":\"gemini-test\",\"payload\":\"expired\"}";
        var recentRaw = "{\"model\":\"gemini-test\",\"payload\":\"recent\"}";
        var expiredSnapshot = "{\"request\":\"expired\"}";
        var expiredRequestedAt = DateTime.UtcNow.AddDays(-31);
        var expiredCompletedAt = expiredRequestedAt.AddSeconds(12);

        Guid expiredId, recentId, alreadyNulledId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var expired = new AIGeneration
            {
                TripId = trip.Id,
                AttemptNumber = 1,
                ModelProvider = "gemini-test",
                InputSnapshot = expiredSnapshot,
                RawOutput = expiredRaw,
                Status = "FAILED_ERROR",
                ValidationErrors = "retention fixture error",
                RequestedAt = expiredRequestedAt,
                CompletedAt = expiredCompletedAt
            };
            var recent = new AIGeneration
            {
                TripId = trip.Id,
                AttemptNumber = 2,
                ModelProvider = "gemini-test",
                InputSnapshot = "{\"request\":\"recent\"}",
                RawOutput = recentRaw,
                Status = "SUCCEEDED",
                RequestedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
            var alreadyNulled = new AIGeneration
            {
                TripId = trip.Id,
                AttemptNumber = 3,
                ModelProvider = "gemini-test",
                InputSnapshot = "{\"request\":\"nulled\"}",
                RawOutput = null,
                Status = "SUCCEEDED",
                RequestedAt = expiredRequestedAt,
                CompletedAt = expiredCompletedAt
            };

            db.AIGenerations.AddRange(expired, recent, alreadyNulled);
            await db.SaveChangesAsync();

            expiredId = expired.Id;
            recentId = recent.Id;
            alreadyNulledId = alreadyNulled.Id;
        }

        // --- Act: default 30-day window ---
        int purged;
        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<IAiRawOutputRetentionService>();
            purged = await service.PurgeExpiredRawOutputsAsync();
        }

        // --- Assert: exactly the expired payload nulled, everything else intact ---
        Assert.Equal(1, purged);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = await db.AIGenerations.AsNoTracking()
                .Where(g => g.TripId == trip.Id)
                .ToListAsync();
            Assert.Equal(3, rows.Count);

            var expiredRow = Assert.Single(rows, r => r.Id == expiredId);
            Assert.Null(expiredRow.RawOutput);
            Assert.Equal("FAILED_ERROR", expiredRow.Status);
            Assert.Equal("retention fixture error", expiredRow.ValidationErrors);
            Assert.Equal(expiredSnapshot, expiredRow.InputSnapshot);
            Assert.Equal(expiredCompletedAt, expiredRow.CompletedAt);

            var recentRow = Assert.Single(rows, r => r.Id == recentId);
            Assert.Equal(recentRaw, recentRow.RawOutput);

            var nulledRow = Assert.Single(rows, r => r.Id == alreadyNulledId);
            Assert.Null(nulledRow.RawOutput);
        }

        // --- Idempotent: a second pass finds nothing to purge ---
        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<IAiRawOutputRetentionService>();
            Assert.Equal(0, await service.PurgeExpiredRawOutputsAsync());
        }

        // --- Disabled window (<= 0) purges nothing ---
        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<IAiRawOutputRetentionService>();
            Assert.Equal(0, await service.PurgeExpiredRawOutputsAsync(retentionDaysOverride: 0));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var recentRow = await db.AIGenerations.AsNoTracking()
                .FirstAsync(g => g.Id == recentId);
            Assert.Equal(recentRaw, recentRow.RawOutput);
        }
    }
}
