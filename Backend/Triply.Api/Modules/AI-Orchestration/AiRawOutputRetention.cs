using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Triply.Api.Data;

namespace Triply.Api.Modules.AIOrchestration;

/// <summary>
/// Data-retention configuration (Gap 4).
/// Section "DataRetention" — see appsettings.Example.json / Docs/configuration.md.
/// </summary>
public sealed class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    /// <summary>
    /// Days an <c>AIGeneration.RawOutput</c> payload is kept before it is purged.
    /// Default 30 — the window documented in Docs/05 §16 (raw AI payload retention)
    /// and DB-D2. A value &lt;= 0 disables the purge.
    /// </summary>
    public int RawOutputDays { get; set; } = 30;
}

/// <summary>
/// Gap 4 — enforces the documented 30-day AI raw-output retention policy
/// (Docs/05 §16: <c>AIGeneration.raw_output</c> "should be purged (nulled out)
/// after a defined retention window … kept only long enough to debug a specific
/// generation, not indefinitely"; audit finding G-06: "stored, never purged").
///
/// Purging means setting <c>RawOutput = NULL</c> — the attempt row itself, its
/// status, validation errors, snapshot and timestamps are preserved, so
/// AIGeneration history/audit semantics (§6.15) are unchanged. Nothing reads
/// RawOutput outside the generation pipeline (verified by repository search), so
/// nulling expired payloads cannot break any query or API response.
/// </summary>
public interface IAiRawOutputRetentionService
{
    /// <summary>
    /// Nulls <c>RawOutput</c> on generation attempts older than the retention
    /// window. Returns the number of rows purged (0 when nothing qualified or the
    /// window is disabled). Idempotent — safe to run repeatedly.
    /// </summary>
    /// <param name="retentionDaysOverride">
    /// Optional explicit window; when null the configured
    /// <c>DataRetention:RawOutputDays</c> (default 30) applies. &lt;= 0 disables the purge.
    /// </param>
    Task<int> PurgeExpiredRawOutputsAsync(
        int? retentionDaysOverride = null,
        CancellationToken cancellationToken = default);
}

public sealed class AiRawOutputRetentionService : IAiRawOutputRetentionService
{
    private readonly ApplicationDbContext _db;
    private readonly DataRetentionOptions _options;
    private readonly ILogger<AiRawOutputRetentionService> _logger;

    public AiRawOutputRetentionService(
        ApplicationDbContext db,
        IOptions<DataRetentionOptions> options,
        ILogger<AiRawOutputRetentionService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> PurgeExpiredRawOutputsAsync(
        int? retentionDaysOverride = null,
        CancellationToken cancellationToken = default)
    {
        var days = retentionDaysOverride ?? _options.RawOutputDays;
        if (days <= 0)
        {
            _logger.LogWarning(
                "AI raw-output retention is disabled (DataRetention:RawOutputDays = {Days}); " +
                "no RawOutput payloads will be purged.", days);
            return 0;
        }

        var cutoff = DateTime.UtcNow.AddDays(-days);

        // Single set-based UPDATE — no row materialization, no tracking, atomic.
        var purged = await _db.AIGenerations
            .Where(g => g.RawOutput != null && g.RequestedAt < cutoff)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(g => g.RawOutput, (string?)null),
                cancellationToken);

        if (purged > 0)
        {
            _logger.LogInformation(
                "AI raw-output retention: purged RawOutput from {Purged} generation attempt(s) " +
                "older than {Days} day(s); attempt rows, statuses and audit columns preserved.",
                purged, days);
        }

        return purged;
    }
}

/// <summary>
/// Hosted runner for <see cref="IAiRawOutputRetentionService"/> — framework-native
/// <c>BackgroundService</c> only (no Hangfire/Quartz/Redis, no new packages).
/// Runs one pass shortly after startup and then every 12 hours. A failed pass is
/// logged and retried on the next interval — it never takes the host down.
/// Inert in the Testing environment: tests invoke the service directly and must
/// not race a background loop.
/// </summary>
public sealed class AiRawOutputRetentionBackgroundService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AiRawOutputRetentionBackgroundService> _logger;

    public AiRawOutputRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        ILogger<AiRawOutputRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_environment.IsEnvironment("Testing"))
            return;

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var retention = scope.ServiceProvider
                    .GetRequiredService<IAiRawOutputRetentionService>();
                await retention.PurgeExpiredRawOutputsAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "AI raw-output retention pass failed; will retry on the next interval.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
