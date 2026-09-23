using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;

namespace Triply.Api.Modules.AIOrchestration;

/// <summary>
/// Loads the source-controlled v2.0.0 trip-plan response schema
/// (<c>AI-Schemas/triply-trip-plan-generation.schema.json</c>) and applies the
/// per-mode <c>destination_options.maxItems</c> required by the AI JSON Schema
/// Contract §5 step 0 (also called out in <c>AI/docs/SCHEMA_CHANGELOG.md</c>)
/// before the schema is handed to Gemini's structured-output mode.
///
/// DESTINATION_FIRST: exactly 1 destination option.
/// BUDGET_FIRST:      1-3 candidate destination options.
///
/// The committed schema file stays a valid, mode-agnostic artifact
/// (minItems 1 / maxItems 3); the mode-specific limit is applied here at
/// request-construction time, so no single universal value is baked into the
/// artifact itself.
/// </summary>
public static class ItineraryGenerationSchema
{
    public const string SchemaFileName = "triply-trip-plan-generation.schema.json";
    public const string SchemaVersion = "2.0.0";

    public const string DestinationFirstMode = "DESTINATION_FIRST";
    public const string BudgetFirstMode = "BUDGET_FIRST";

    /// <summary>
    /// The destination option cap for a planning mode (Contract §4.1 / §5 step 0).
    /// Anything other than DESTINATION_FIRST uses the BUDGET_FIRST cap of 3.
    /// </summary>
    public static int MaxDestinationOptions(string? planningMode) =>
        string.Equals(planningMode, DestinationFirstMode, StringComparison.Ordinal) ? 1 : 3;

    /// <summary>
    /// Reads <c>AI-Schemas/{SchemaFileName}</c>, sets
    /// <c>destination_options.maxItems</c> for <paramref name="planningMode"/>,
    /// and returns the adjusted schema as a <see cref="JsonDocument"/>. Every
    /// other rule in the schema is preserved unchanged.
    /// </summary>
    public static JsonDocument LoadForMode(IHostEnvironment environment, string? planningMode)
    {
        var path = Path.Combine(environment.ContentRootPath, "AI-Schemas", SchemaFileName);
        if (!File.Exists(path))
            throw new InvalidOperationException($"AI response schema was not found at '{path}'.");

        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidOperationException($"AI response schema at '{path}' is not a JSON object.");

        if (root["properties"]?["destination_options"] is not JsonObject destinationOptions)
            throw new InvalidOperationException(
                $"AI response schema at '{path}' is missing properties.destination_options.");

        destinationOptions["maxItems"] = MaxDestinationOptions(planningMode);

        return JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(root));
    }
}
