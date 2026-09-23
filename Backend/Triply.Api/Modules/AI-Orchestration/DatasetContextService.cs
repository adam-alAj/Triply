using System.Text;

namespace Triply.Api.Modules.AIOrchestration;

public interface IExtraAiContextReader
{
    Task<IReadOnlyDictionary<string, string>> ReadBudgetTiersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the AI track's Extra_AI_Context.csv without adding a new runtime dataset.
/// The file is intentionally external to the API project because it is owned by the
/// AI dataset track. Missing/empty data is reported explicitly for BUDGET_FIRST.
/// </summary>
public sealed class ExtraAiContextReader : IExtraAiContextReader
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public ExtraAiContextReader(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<IReadOnlyDictionary<string, string>> ReadBudgetTiersAsync(
        CancellationToken cancellationToken = default)
    {
        var configured = _configuration["AI:ExtraAiContextPath"];
        var path = ResolvePath(configured);

        if (!File.Exists(path))
            throw new InvalidOperationException(
                $"Extra_AI_Context.csv was not found at '{path}'. " +
                "BUDGET_FIRST generation requires the AI dataset context file.");

        var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8, cancellationToken);
        if (lines.Length == 0 || lines.All(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(
                "Extra_AI_Context.csv is empty. Populate budget_tier values before using BUDGET_FIRST generation.");

        var header = ParseCsvLine(lines[0]);
        var budgetTierIndex = FindColumn(header, "budget_tier");
        if (budgetTierIndex < 0)
            throw new InvalidOperationException(
                "Extra_AI_Context.csv must contain a 'budget_tier' column.");

        // The CSV is the AI track's file and its id column is named `source_place_id`.
        // Rather than picking one identifier shape, index every row under BOTH its id
        // (when present) and its place name: AiOrchestrationService looks a place up by
        // `Place.id` first and falls back to `Place.name`, so either keying resolves and
        // neither a renamed id column nor a renamed place can silently yield no tier.
        var idIndex = FindFirstColumn(header, "place_id", "id", "source_place_id");
        var nameIndex = FindFirstColumn(header, "place_name", "name");

        if (idIndex < 0 && nameIndex < 0)
            throw new InvalidOperationException(
                "Extra_AI_Context.csv must contain a place identifier column " +
                "('place_id'/'id'/'source_place_id') or a place name column ('place_name'/'name').");

        var widestIndex = new[] { budgetTierIndex, idIndex, nameIndex }.Max();

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[lineIndex])) continue;

            var row = ParseCsvLine(lines[lineIndex]);
            if (row.Count <= widestIndex) continue;

            var tier = row[budgetTierIndex].Trim();
            if (tier.Length == 0) continue;

            if (idIndex >= 0)
            {
                var id = row[idIndex].Trim();
                if (id.Length > 0) result[id] = tier;
            }

            if (nameIndex >= 0)
            {
                var name = row[nameIndex].Trim();
                if (name.Length > 0) result[name] = tier;
            }
        }

        return result;
    }

    private string ResolvePath(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, configured));

        // Local repository layout: Backend/Triply.Api -> Backend -> repo root -> AI dataset.
        return Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            "..", "..", "AI", "01-Dataset", "curated-data", "Extra_AI_Context.csv"));
    }

    private static int FindColumn(IReadOnlyList<string> header, string name) =>
        header.ToList().FindIndex(x => string.Equals(x.Trim(), name, StringComparison.OrdinalIgnoreCase));

    private static int FindFirstColumn(IReadOnlyList<string> header, params string[] names)
    {
        foreach (var name in names)
        {
            var index = FindColumn(header, name);
            if (index >= 0) return index;
        }
        return -1;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (ch == ',' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values;
    }
}
