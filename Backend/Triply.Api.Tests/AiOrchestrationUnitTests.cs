using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;
using Moq;
using Moq.Protected;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration;
using Triply.Api.Modules.AIOrchestration.Dtos;

namespace Triply.Api.Tests;

public class AiOrchestrationUnitTests
{
    private readonly ITestOutputHelper _output;

    public AiOrchestrationUnitTests(ITestOutputHelper output)
    {
        _output = output;
    }

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
            Times.Once(),                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Post &&
                    request.RequestUri!.ToString().Contains("test-model:generateContent") &&
                    !request.RequestUri!.Query.Contains("key=")),
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

    [Fact]
    public async Task GeminiClient_SendsApiKeyViaHeader_NeverInUrl()
    {
        var handler = new RecordingHandler(
            """{"candidates":[{"content":{"parts":[{"text":"{\"ok\":true}"}]}}]}""");

        using var http = new HttpClient(handler);
        var logger = new CapturingGeminiLogger();
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "test-key",
            Model = "test-model",
            BaseUrl = "https://example.test/models",
            TimeoutSeconds = 5
        });

        var client = new GeminiClient(http, options, logger);

        await client.GenerateJsonAsync("test prompt");

        Assert.NotNull(handler.LastRequest);
        var uri = handler.LastRequest!.RequestUri!;
        Assert.Equal("/models/test-model:generateContent", uri.AbsolutePath);
        Assert.DoesNotContain("test-key", uri.ToString());
        Assert.DoesNotContain("key=", uri.Query);
        Assert.True(handler.LastRequest.Headers.TryGetValues("x-goog-api-key", out var apiKeys));
        Assert.Equal("test-key", apiKeys!.Single());

        // The structured-output path must use the same header-based auth.
        handler.LastRequest = null;
        using var schema = JsonDocument.Parse("""{"type":"object"}""");
        await client.GenerateJsonWithSchemaAsync("test prompt", "system", schema);

        Assert.NotNull(handler.LastRequest);
        var schemaUri = handler.LastRequest!.RequestUri!;
        Assert.DoesNotContain("test-key", schemaUri.ToString());
        Assert.DoesNotContain("key=", schemaUri.Query);
        Assert.True(handler.LastRequest.Headers.TryGetValues("x-goog-api-key", out var schemaApiKeys));
        Assert.Equal("test-key", schemaApiKeys!.Single());

        var authLogs = logger.Messages
            .Where(message => message.Contains("x-goog-api-key", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, authLogs.Count);
        foreach (var authLog in authLogs)
        {
            Assert.Contains("using x-goog-api-key header (key redacted)", authLog, StringComparison.Ordinal);
            Assert.DoesNotContain("test-key", authLog, StringComparison.Ordinal);
            Assert.DoesNotContain("?key=", authLog, StringComparison.OrdinalIgnoreCase);
            _output.WriteLine(authLog);
        }
    }

    // --- Contract §5 step 0: destination_options.maxItems is mode-specific ---

    [Fact]
    public void ItineraryGenerationSchema_DestinationFirst_SetsMaxItemsToOne()
    {
        using var schema = ItineraryGenerationSchema.LoadForMode(
            TestEnvironment(), ItineraryGenerationSchema.DestinationFirstMode);

        Assert.Equal(1, DestinationOptionsMaxItems(schema));
    }

    [Fact]
    public void ItineraryGenerationSchema_BudgetFirst_SetsMaxItemsToThree()
    {
        using var schema = ItineraryGenerationSchema.LoadForMode(
            TestEnvironment(), ItineraryGenerationSchema.BudgetFirstMode);

        Assert.Equal(3, DestinationOptionsMaxItems(schema));
    }

    [Fact]
    public void ItineraryGenerationSchema_UnknownMode_FallsBackToBudgetFirstCap()
    {
        using var schema = ItineraryGenerationSchema.LoadForMode(TestEnvironment(), "SOMETHING_ELSE");

        Assert.Equal(3, DestinationOptionsMaxItems(schema));
    }

    [Fact]
    public void ItineraryGenerationSchema_PreservesMinItemsOne()
    {
        using var schema = ItineraryGenerationSchema.LoadForMode(
            TestEnvironment(), ItineraryGenerationSchema.BudgetFirstMode);

        var destinationOptions = schema.RootElement
            .GetProperty("properties")
            .GetProperty("destination_options");

        Assert.Equal(1, destinationOptions.GetProperty("minItems").GetInt32());
    }

    private static IHostEnvironment TestEnvironment()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);
        return environment.Object;
    }

    private static int DestinationOptionsMaxItems(JsonDocument schema) =>
        schema.RootElement
            .GetProperty("properties")
            .GetProperty("destination_options")
            .GetProperty("maxItems")
            .GetInt32();

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public RecordingHandler(string responseJson) => _responseJson = responseJson;

        public HttpRequestMessage? LastRequest { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CapturingGeminiLogger : ILogger<GeminiClient>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
