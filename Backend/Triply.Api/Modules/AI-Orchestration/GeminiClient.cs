using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Triply.Api.Modules.AIOrchestration;

/// <summary>
/// Thin, testable wrapper around the raw Gemini REST call.
/// Architecture §4 ADR-01: the Backend module makes the live HTTPS call to Gemini.
///
/// v2.0.0 — supports responseJsonSchema for structured output enforcement.
/// The schema is loaded from the finalized triply-trip-plan-generation.schema.json
/// and passed to Gemini's generationConfig to guarantee JSON structure.
/// </summary>
public interface IGeminiClient
{
    /// <summary>
    /// Sends a prompt to Gemini and returns the raw JSON text of its response.
    /// </summary>
    Task<string> GenerateJsonAsync(
        string prompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a prompt with a response schema to Gemini for structured output.
    /// The schema enforces the JSON shape Gemini must return.
    /// </summary>
    Task<string> GenerateJsonWithSchemaAsync(
        string prompt,
        string systemInstruction,
        JsonDocument schema,
        CancellationToken cancellationToken = default);
}

public class GeminiClient : IGeminiClient
{
    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GeminiClient(
        HttpClient http,
        IOptions<GeminiOptions> options,
        ILogger<GeminiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (_http.Timeout == Timeout.InfiniteTimeSpan)
        {
            _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    public async Task<string> GenerateJsonAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) ||
            _options.ApiKey.StartsWith(
                "REPLACE_WITH",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Gemini:ApiKey is not configured. Set it via environment variable / secrets, never commit it.");
        }

        var url = $"{_options.BaseUrl}/{_options.Model}:generateContent";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            text = prompt
                        }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.4
            }
        };

        return await SendRequestAsync(
            url,
            requestBody,
            cancellationToken);
    }

    /// <summary>
    /// Sends a prompt with a responseJsonSchema to Gemini.
    /// This enforces structured output matching the v2.0.0 contract schema.
    /// </summary>
    public async Task<string> GenerateJsonWithSchemaAsync(
        string prompt,
        string systemInstruction,
        JsonDocument schema,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) ||
            _options.ApiKey.StartsWith(
                "REPLACE_WITH",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Gemini:ApiKey is not configured. Set it via environment variable / secrets, never commit it.");
        }

        var url = $"{_options.BaseUrl}/{_options.Model}:generateContent";

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = systemInstruction
                    }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            text = prompt
                        }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseJsonSchema = schema.RootElement,
                temperature = 0.4
            }
        };

        return await SendRequestAsync(
            url,
            requestBody,
            cancellationToken);
    }

    private async Task<string> SendRequestAsync(
        string url,
        object requestBody,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            var json = JsonSerializer.Serialize(
                requestBody,
                JsonOptions);

            var jsonBytes = Encoding.UTF8.GetBytes(json);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                url);

            var content = new ByteArrayContent(jsonBytes);

            content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json");

            content.Headers.ContentLength =
                jsonBytes.Length;

            request.Content = content;

            request.Headers.TransferEncodingChunked = false;

            request.Version = HttpVersion.Version11;
            request.VersionPolicy =
                HttpVersionPolicy.RequestVersionExact;

            _logger.LogInformation(
                "Gemini HTTP request: ContentLength={ContentLength}, Chunked={Chunked}, Version={Version}",
                request.Content.Headers.ContentLength,
                request.Headers.TransferEncodingChunked,
                request.Version);

            request.Headers.TryAddWithoutValidation(
                "x-goog-api-key",
                _options.ApiKey);

            _logger.LogInformation(
                "Sending Gemini API request to {RequestPath} using {ApiKeyHeader} header (key redacted)",
                request.RequestUri!.GetLeftPart(UriPartial.Path),
                "x-goog-api-key");

            response = await _http.SendAsync(
                request,
                cancellationToken);
        }
        catch (TaskCanceledException ex)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new GeminiApiException(
                "Gemini API call timed out.",
                string.Empty,
                ex);
        }

        var raw = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Gemini API call failed with {StatusCode}: {Body}",
                response.StatusCode,
                raw);

            throw new GeminiApiException(
                $"Gemini API returned {(int)response.StatusCode} {response.StatusCode}.",
                raw);
        }

        string? text;

        try
        {
            using var doc = JsonDocument.Parse(raw);

            text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
        }
        catch (Exception ex)
        {
            throw new GeminiApiException(
                "Gemini API returned an unexpected response format.",
                raw,
                ex);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new GeminiApiException(
                "Gemini API returned an empty response.",
                raw);
        }

        return text;
    }
}

/// <summary>
/// Raised for any failure calling or parsing the Gemini API envelope itself
/// (network, non-2xx, malformed envelope) — distinct from itinerary content
/// validation, which is handled by IItineraryValidator against the internal dataset.
/// </summary>
public class GeminiApiException : Exception
{
    public string RawResponse { get; }

    public GeminiApiException(
        string message,
        string rawResponse,
        Exception? inner = null)
        : base(message, inner)
    {
        RawResponse = rawResponse;
    }
}