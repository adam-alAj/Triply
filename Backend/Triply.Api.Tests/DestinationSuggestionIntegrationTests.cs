using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.Auth.Dtos;
using Triply.Api.Modules.Destination.Dtos;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace Triply.Api.Tests;

public class DestinationSuggestionIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DestinationSuggestionIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail()
        => $"suggest_{Guid.NewGuid():N}@triply.dev";

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                UniqueEmail(),
                "P@ssw0rd123",
                "Suggestion Test User"));

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private async Task<long> SeedDestinationWithPlacesAsync(
        long currencyId,
        decimal firstPrice,
        decimal secondPrice,
        long firstInterestCategoryId = 1,
        long secondInterestCategoryId = 2)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var destination = new Destination
        {
            CountryId = 1,
            Name = $"SuggestionDestination_{Guid.NewGuid():N}",
            Description = "Integration-test destination",
            IsSupported = true
        };
        db.Destinations.Add(destination);
        await db.SaveChangesAsync();

        var placeCategoryId = await db.PlaceCategories
            .Select(x => x.Id)
            .FirstAsync();

        var costCategoryId = await db.CostCategories
            .Select(x => x.Id)
            .FirstAsync();

        var firstPlace = new Place
        {
            DestinationId = destination.Id,
            PlaceCategoryId = placeCategoryId,
            Name = $"SuggestionPlaceA_{Guid.NewGuid():N}",
            ReferencePrice = firstPrice,
            CurrencyId = currencyId,
            CostCategoryId = costCategoryId,
            IsActive = true
        };

        var secondPlace = new Place
        {
            DestinationId = destination.Id,
            PlaceCategoryId = placeCategoryId,
            Name = $"SuggestionPlaceB_{Guid.NewGuid():N}",
            ReferencePrice = secondPrice,
            CurrencyId = currencyId,
            CostCategoryId = costCategoryId,
            IsActive = true
        };

        db.Places.AddRange(firstPlace, secondPlace);
        await db.SaveChangesAsync();

        db.PlaceInterests.AddRange(
            new PlaceInterest
            {
                PlaceId = firstPlace.Id,
                InterestCategoryId = firstInterestCategoryId
            },
            new PlaceInterest
            {
                PlaceId = secondPlace.Id,
                InterestCategoryId = secondInterestCategoryId
            });

        await db.SaveChangesAsync();
        return destination.Id;
    }

    private async Task<long> CreateUniqueInterestCategoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var category = new InterestCategory
        {
            Code = $"TEST_{Guid.NewGuid():N}",
            Label = "Integration Test Interest"
        };

        db.InterestCategories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    [Fact]
    public async Task Suggestions_WithRealisticBudgetAndInterest_ReturnsCandidate()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Use a unique interest for this test so destinations created by other
        // integration tests cannot displace this candidate from the top three.
        var testInterestId = await CreateUniqueInterestCategoryAsync();

        var destinationId = await SeedDestinationWithPlacesAsync(
            currencyId: 1,
            firstPrice: 250,
            secondPrice: 350,
            firstInterestCategoryId: testInterestId,
            secondInterestCategoryId: testInterestId);

        var response = await _client.PostAsJsonAsync(
            "/api/destinations/suggestions",
            new DestinationSuggestionRequest
            {
                BudgetAmount = 700,
                BudgetCurrencyId = 1,
                InterestCategoryIds = new List<long> { testInterestId }
            });

        response.EnsureSuccessStatusCode();

        var body = await response.Content
            .ReadFromJsonAsync<DestinationSuggestionEnvelope>();

        Assert.NotNull(body);
        Assert.Equal(1, body!.Count);
        Assert.Contains(
            body.Suggestions,
            x => x.DestinationId == destinationId &&
                 x.EstimatedCost == 600 &&
                 x.Currency == "USD" &&
                 x.IsEstimated);
    }

    [Fact]
    public async Task Suggestions_WhenNoDestinationFitsBudget_ReturnsEmptySuggestions()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        await SeedDestinationWithPlacesAsync(
            currencyId: 1,
            firstPrice: 800,
            secondPrice: 500);

        var response = await _client.PostAsJsonAsync(
            "/api/destinations/suggestions",
            new DestinationSuggestionRequest
            {
                BudgetAmount = 100,
                BudgetCurrencyId = 1,
                InterestCategoryIds = new List<long> { 3 }
            });

        response.EnsureSuccessStatusCode();

        var body = await response.Content
            .ReadFromJsonAsync<DestinationSuggestionEnvelope>();

        Assert.NotNull(body);
        Assert.Empty(body!.Suggestions);
    }

    [Fact]
    public async Task Suggestions_WithNoInterestOverlap_ExcludesDestination()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var destinationId = await SeedDestinationWithPlacesAsync(
            currencyId: 1,
            firstPrice: 100,
            secondPrice: 100);

        var response = await _client.PostAsJsonAsync(
            "/api/destinations/suggestions",
            new DestinationSuggestionRequest
            {
                BudgetAmount = 500,
                BudgetCurrencyId = 1,
                InterestCategoryIds = new List<long> { 3 }
            });

        response.EnsureSuccessStatusCode();

        var body = await response.Content
            .ReadFromJsonAsync<DestinationSuggestionEnvelope>();

        Assert.NotNull(body);
        Assert.Equal(0, body!.Count);
        Assert.DoesNotContain(
            body.Suggestions,
            x => x.DestinationId == destinationId);
    }

    [Fact]
    public async Task Suggestions_WithMultipleInterestMatches_RanksByMatchCountBeforeCost()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var lowerCostDestination = await SeedDestinationWithPlacesAsync(
            currencyId: 1,
            firstPrice: 100,
            secondPrice: 100);

        var higherCostDestination = await SeedDestinationWithPlacesAsync(
            currencyId: 1,
            firstPrice: 300,
            secondPrice: 300);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var secondDestinationPlaceIds = await db.Places
                .Where(p => p.DestinationId == higherCostDestination)
                .Select(p => p.Id)
                .ToListAsync();

            db.PlaceInterests.Add(
                new PlaceInterest
                {
                    PlaceId = secondDestinationPlaceIds[1],
                    InterestCategoryId = 1
                });

            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync(
            "/api/destinations/suggestions",
            new DestinationSuggestionRequest
            {
                BudgetAmount = 1000,
                BudgetCurrencyId = 1,
                InterestCategoryIds = new List<long> { 1, 2 }
            });

        response.EnsureSuccessStatusCode();

        var body = await response.Content
            .ReadFromJsonAsync<DestinationSuggestionEnvelope>();

        Assert.NotNull(body);
        Assert.True(body!.Count >= 2);

        var first = body.Suggestions
            .First(x => x.DestinationId == lowerCostDestination);
        var second = body.Suggestions
            .First(x => x.DestinationId == higherCostDestination);

        Assert.True(
            body.Suggestions.IndexOf(first) <
            body.Suggestions.IndexOf(second));
    }

    [Fact]
    public async Task Suggestions_ReturnsAtMostThreeClosestAlternatives()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        for (var i = 0; i < 4; i++)
        {
            await SeedDestinationWithPlacesAsync(
                currencyId: 1,
                firstPrice: 400 + (i * 100),
                secondPrice: 400);
        }

        var response = await _client.PostAsJsonAsync(
            "/api/destinations/suggestions",
            new DestinationSuggestionRequest
            {
                BudgetAmount = 1000,
                BudgetCurrencyId = 1,
                InterestCategoryIds = new List<long> { 1, 2 }
            });

        response.EnsureSuccessStatusCode();

        var body = await response.Content
            .ReadFromJsonAsync<DestinationSuggestionEnvelope>();

        Assert.NotNull(body);
        Assert.Equal(3, body!.Count);
        Assert.Equal(3, body.Suggestions.Count);
    }

    [Fact]
    public async Task Suggestions_WithUnknownInterest_ReturnsFieldLevelBadRequest()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync(
            "/api/destinations/suggestions",
            new DestinationSuggestionRequest
            {
                BudgetAmount = 700,
                BudgetCurrencyId = 1,
                InterestCategoryIds = new List<long> { 999999 }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
Assert.Contains("interestCategoryIds", body);    }

    private sealed class DestinationSuggestionEnvelope
    {
        public List<DestinationSuggestionResponse> Suggestions { get; set; } = new();
        public int Count { get; set; }
    }
}
