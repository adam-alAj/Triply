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
public string Model { get; set; } = "gemini-3.6-flash";
 public string BaseUrl { get; set; } =
        "https://generativelanguage.googleapis.com/v1beta/models";

    // Bounded regeneration retries — SRS FR-AI-002 / Architecture §9 "alt Invalid" branch.
    // Total attempts = MaxRetries + 1 (the original attempt).
    public int MaxRetries { get; set; } = 2;

    public int TimeoutSeconds { get; set; } = 30;

    // NOTE: the former CostTolerancePercent (SRS §17 D1, ±15%) was removed as dead
    // configuration — AI JSON Schema Contract v2.0.0 dropped the AI-echoed cost
    // summary, so nothing ever consumed it. Cost is always recomputed
    // deterministically from Place.reference_price (Database Design §15).
}
