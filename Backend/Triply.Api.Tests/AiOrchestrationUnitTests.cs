using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration;
using Triply.Api.Modules.AIOrchestration.Dtos;

namespace Triply.Api.Tests;

public class AiOrchestrationUnitTests
{
    [Fact]
    public void PromptBuilder_DestinationFirst_ContainsGroundedNamesAndExactDates()
    {
        var builder = new ItineraryPromptBuilder();
        var trip = new Trip
        {
            PlanningMode = "DESTINATION_FIRST",
            DestinationId = 1,
            TravelerCount = 2,
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 3),
            BudgetAmount = 1000
        };

        var places = new List<PlaceContextDto>
        {
            new()
            {
                Id = 1,
                DestinationId = 1,
                DestinationName = "Jerusalem",
                Name = "Grounded Restaurant",
                Category = "RESTAURANT",
                ReferencePrice = 25,
                Currency = "USD"
            },
            new()
            {
                Id = 2,
                DestinationId = 1,
                DestinationName = "Jerusalem",
                Name = "Grounded Hotel",
                Category = "ACCOMMODATION",
                ReferencePrice = 100,
                Currency = "USD"
            }
        };

        var prompt = builder.Build(trip, places, ["Food"], 3);

        Assert.Contains("Grounded Restaurant", prompt);
        Assert.Contains("Grounded Hotel", prompt);
        Assert.Contains("day_number 1 => date \"2026-10-01\"", prompt);
        Assert.Contains("day_number 3 => date \"2026-10-03\"", prompt);
        Assert.Contains("Never output any database ID", prompt);
    }

    [Fact]
    public async Task GeminiClient_ParsesExpectedEnvelope()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"candidates":[{"content":{"parts":[{"text":"{\"ok\":true}"}]}}]}""",
                    Encoding.UTF8,
                    "application/json")
            });

        using var http = new HttpClient(handler.Object);
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "test-key",
            Model = "test-model",
            BaseUrl = "https://example.test/models",
            TimeoutSeconds = 5
        });

        var client = new GeminiClient(
            http,
            options,
            NullLogger<GeminiClient>.Instance);

        var result = await client.GenerateJsonAsync("test prompt");

        Assert.Equal("{\"ok\":true}", result);

        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(request =>
                request.Method == HttpMethod.Post &&
                request.RequestUri!.ToString().Contains("test-model:generateContent?key=test-key")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GeminiClient_NonSuccess_ThrowsGeminiApiException()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{"error":"rate limited"}""")
            });

        using var http = new HttpClient(handler.Object);
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "test-key",
            Model = "test-model",
            BaseUrl = "https://example.test/models"
        });

        var client = new GeminiClient(
            http,
            options,
            NullLogger<GeminiClient>.Instance);

        var ex = await Assert.ThrowsAsync<GeminiApiException>(
            () => client.GenerateJsonAsync("test prompt"));

        Assert.Contains("429", ex.Message);
        Assert.Contains("rate limited", ex.RawResponse);
    }
}
