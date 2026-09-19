namespace Triply.Api.Modules.AIOrchestration;

/// <summary>
/// Configuration for the Gemini integration (Architecture §4 ADR-01: Backend owns this call).
/// Bound from the "Gemini" section in appsettings / environment variables — never hardcode the ApiKey.
/// </summary>
public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = default!;

    // Flash-family model per the Recommended Technology Stack (free tier). Confirm exact model
    // name with the AI track once Latency Testing (Gemini Flash vs Flash-Lite) concludes.
public string Model { get; set; } = "gemini-flash-lite-latest";
 public string BaseUrl { get; set; } =
        "https://generativelanguage.googleapis.com/v1beta/models";

    // Bounded regeneration retries — SRS FR-AI-002 / Architecture §9 "alt Invalid" branch.
    // Total attempts = MaxRetries + 1 (the original attempt).
    public int MaxRetries { get; set; } = 2;

    public int TimeoutSeconds { get; set; } = 30;

    // SRS §17 D1 — proposed ±15%, pending team sign-off. Used only if the AI-agreed
    // output schema ever echoes back a price to sanity-check against Place.reference_price;
    // the persisted cost is always recomputed deterministically from Place.reference_price
    // regardless (Database Design §15), so this never controls what gets stored.
    public decimal CostTolerancePercent { get; set; } = 15m;
}
