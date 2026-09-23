namespace Triply.Api.Tests;

/// <summary>
/// The generated-schema artifact is committed twice — once by the AI track
/// (AI/02-Prompt-Engineering/json-schemas) and once by the Backend
/// (AI-Schemas), which is the copy actually sent to Gemini. Nothing links the
/// two files, so editing only one silently changes what the model is asked to
/// produce while validation keeps checking the contract the model never saw.
///
/// The Python harness enforces the same invariant in
/// AI/03-Validation/validate_shared_fixtures.py, so the guard also runs on
/// machines without the .NET SDK.
/// </summary>
public class SchemaFileParityTests
{
    private const string SchemaFileName = "triply-trip-plan-generation.schema.json";

    [Fact]
    public void GenerationSchemaCopies_AreByteIdentical()
    {
        var aiTrackSchema = ResolveFromRepositoryRoot(
            "AI", "02-Prompt-Engineering", "json-schemas", SchemaFileName);
        var backendSchema = ResolveFromRepositoryRoot(
            "Backend", "Triply.Api", "AI-Schemas", SchemaFileName);

        Assert.True(File.Exists(aiTrackSchema), $"AI-track schema not found at '{aiTrackSchema}'.");
        Assert.True(File.Exists(backendSchema), $"Backend schema not found at '{backendSchema}'.");

        var aiTrackBytes = File.ReadAllBytes(aiTrackSchema);
        var backendBytes = File.ReadAllBytes(backendSchema);

        Assert.True(
            aiTrackBytes.AsSpan().SequenceEqual(backendBytes),
            "The two copies of the trip-plan generation schema have drifted apart. " +
            $"Update both '{aiTrackSchema}' and '{backendSchema}' so the Backend sends " +
            "Gemini exactly the schema the validation rules describe.");
    }

    /// <summary>
    /// Walks up from the test output directory to the repository root, so the test
    /// does not depend on the target framework folder layout.
    /// </summary>
    private static string ResolveFromRepositoryRoot(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName }.Concat(relativeSegments).ToArray());

            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        // Report the layout the test expected from the output directory.
        return Path.Combine(
            new[] { AppContext.BaseDirectory }.Concat(relativeSegments).ToArray());
    }
}
