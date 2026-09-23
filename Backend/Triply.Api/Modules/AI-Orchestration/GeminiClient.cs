using System.Net.Http.Json;
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
    /// <summary>Sends a prompt to Gemini and returns the raw JSON text of its response.</summary>
    Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default);

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
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GeminiClient(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (_http.Timeout == Timeout.InfiniteTimeSpan)
            _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) ||
            _options.ApiKey.StartsWith("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Gemini:ApiKey is not configured. Set it via environment variable / secrets, never commit it.");
        }

        var url = $"{_options.BaseUrl}/{_options.Model}:generateContent";

        // responseMimeType=application/json asks Gemini's structured-output mode to return
        // JSON only, no surrounding prose — reduces (does not replace) the need for validation.
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.4
            }
        };

        return await SendRequestAsync(url, requestBody, cancellationToken);
    }

    /// <summary>
    /// Sends a prompt with a responseJsonSchema to Gemini.
    /// This enforces structured output matching the v2.0.0 contract schema.
    ///
    /// Per Gemini API docs, responseJsonSchema accepts a full JSON Schema (draft 2020-12)
    /// including $defs, $ref, and all standard keywords.
    /// The maxItems for destination_options should be set on the schema before calling
    /// this method (per contract §5, step 0).
    /// </summary>
    public async Task<string> GenerateJsonWithSchemaAsync(
        string prompt,
        string systemInstruction,
        JsonDocument schema,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) ||
            _options.ApiKey.StartsWith("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Gemini:ApiKey is not configured. Set it via environment variable / secrets, never commit it.");
        }

        var url = $"{_options.BaseUrl}/{_options.Model}:generateContent";

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemInstruction } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseJsonSchema = schema.RootElement,
                temperature = 0.4
            }
        };

        return await SendRequestAsync(url, requestBody, cancellationToken);
    }

    private async Task<string> SendRequestAsync(
        string url,
        object requestBody,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            // Auth via the x-goog-api-key header (Google's documented API-key
            // authentication header), never via the query string: a ?key= URL leaks
            // the secret into HTTP request logs, proxies, and tracing output
            // (Gap 4 / audit R-05). Model selection, payload, parsing, timeout and
            // retry behavior are unchanged.
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(requestBody, options: JsonOptions)
            };
            request.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);

            _logger.LogInformation(
                "Sending Gemini API request to {RequestPath} using {ApiKeyHeader} header (key redacted)",
                request.RequestUri!.GetLeftPart(UriPartial.Path),
                "x-goog-api-key");

            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GeminiApiException("Gemini API call timed out.", string.Empty, ex);
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

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
        catch (Exception ex) when (ex is KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException or JsonException)
        {
            _logger.LogError(ex, "Unexpected Gemini response envelope: {Body}", raw);
            throw new GeminiApiException("Gemini response did not match the expected envelope shape.", raw, ex);
        }

        if (string.IsNullOrWhiteSpace(text))
            throw new GeminiApiException("Gemini returned an empty candidate.", raw);

        return text;
    }
}

/// <summary>Raised for any failure calling or parsing the Gemini API envelope itself
/// (network, non-2xx, malformed envelope) — distinct from itinerary *content* validation,
/// which is handled by IItineraryValidator against the internal dataset.</summary>
public class GeminiApiException : Exception
{
    public string RawResponse { get; }

    public GeminiApiException(string message, string rawResponse, Exception? inner = null)
        : base(message, inner)
    {
        RawResponse = rawResponse;
    }
}
