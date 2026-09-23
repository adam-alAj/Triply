using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Triply.Api.Common.Authorization;
using Triply.Api.Common.Middleware;
using Triply.Api.Data;
using Triply.Api.Entities;
using Triply.Api.Modules.AIOrchestration;
using Triply.Api.Modules.Auth;
using Triply.Api.Modules.Cost;
using Triply.Api.Modules.Destination;
using Triply.Api.Modules.Trip.Validators;
using Triply.Api.Modules.Currency;


var builder = WebApplication.CreateBuilder(args);

// ---------- Monitoring / structured request logging ----------
// Staging uses JSON console logging so ILogger events are machine-readable and
// can be collected by the hosting platform. Request logging is enabled only in
// Staging so local development/test output stays focused.
if (builder.Environment.IsStaging())
{
    builder.Services.AddHttpLogging(options =>
    {
        options.LoggingFields =
            Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod |
            Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath |
            Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode |
            Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration;
    });
}

// ---------- Configuration ----------
// Connection string + JWT key + Gemini key all come from configuration/env vars,
// never hardcoded. See .env.example / appsettings.Example.json.

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["ConnectionStrings__Default"]
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured.");

// ---------- Request size limit (Security Task 2) ----------
// Nothing previously stopped a client from sending an oversized request body
// (Kestrel's own default is ~28.6 MB — far larger than anything this API needs).
// 1 MB comfortably covers every JSON payload this API accepts today.
const long MaxRequestBodyBytes = 1 * 1024 * 1024;

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
});

// ---------- EF Core + SQL Server ----------

builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseSqlServer(connectionString));

// ---------- Identity ----------

builder.Services
    .AddIdentityCore<ApplicationUser>(opt =>
    {
        opt.Password.RequiredLength = 8;
        opt.Password.RequireUppercase = true;
        opt.Password.RequireDigit = true;
        opt.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    // Needed for GenerateEmailConfirmationTokenAsync / GeneratePasswordResetTokenAsync
    // (Security Task 1: email verification + password reset).
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();

// ---------- JWT Authentication ----------

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Triply";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TriplyClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey))
    };
});

// ---------- Ownership Authorization ----------

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TripOwner", policy =>
        policy.Requirements.Add(new TripOwnerRequirement()));
});

builder.Services.AddScoped<IAuthorizationHandler, TripOwnerHandler>();

// ---------- FluentValidation ----------

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ---------- Rate Limiting ----------
// Every limiter below is partitioned per caller (authenticated user id, falling back
// to remote IP for anonymous requests like login/register) instead of one shared
// global counter — otherwise one abusive client throttles every other user at once
// (Security Task 3, confirmed by SecurityHardeningTests / RateLimitingTests).
static string PartitionKey(HttpContext context)
{
    var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? context.User.FindFirst("sub")?.Value;

    if (!string.IsNullOrEmpty(userId))
        return $"user:{userId}";

    return $"ip:{context.Connection.RemoteIpAddress}";
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // General API limit: 10 requests/minute in production,
    // 200 requests/minute in Testing so the integration suite is not throttled.
    var generalPermitLimit =
        builder.Environment.IsEnvironment("Testing") ? 200 : 10;

    options.AddPolicy("fixed", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = generalPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // AI generation is the most expensive action in the app (costs money + time),
    // so it gets its own, much tighter, per-user budget instead of sharing "fixed".
    // Security Task 3: 10 generations per hour per user.
    options.AddPolicy("ai-generation", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit =
                    builder.Environment.IsEnvironment("Testing") ? 100 : 10,
                Window = TimeSpan.FromHours(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});
// ---------- CORS ----------

var flutterOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("FlutterClients", policy =>
        policy.WithOrigins(flutterOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ---------- Controllers / Swagger ----------

builder.Services.AddScoped<ICostAggregationService, CostAggregationService>();

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IDestinationSuggestionService, DestinationSuggestionService>();
builder.Services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();

// ---------- AI-Orchestration (Gemini) — Architecture §4 ADR-01 ----------

builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

builder.Services.AddHttpClient<IGeminiClient, GeminiClient>();
builder.Services.AddScoped<IItineraryPromptBuilder, ItineraryPromptBuilder>();
builder.Services.AddScoped<IItineraryValidator, ItineraryValidationService>();
builder.Services.AddScoped<IExtraAiContextReader, ExtraAiContextReader>();
builder.Services.AddScoped<IAiOrchestrationService, AiOrchestrationService>();

// Gap 4 — 30-day AI raw-output retention (Docs/05 §16): nulls
// AIGeneration.RawOutput for attempts older than DataRetention:RawOutputDays
// (default 30). The hosted runner is inert in the Testing environment — tests
// call IAiRawOutputRetentionService directly.
builder.Services.Configure<DataRetentionOptions>(
    builder.Configuration.GetSection(DataRetentionOptions.SectionName));
builder.Services.AddScoped<IAiRawOutputRetentionService, AiRawOutputRetentionService>();
builder.Services.AddHostedService<AiRawOutputRetentionBackgroundService>();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ---------- Centralized Error Handling ----------

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Structured request/response telemetry for staging smoke tests.
if (app.Environment.IsStaging())
{
    app.UseHttpLogging();
}

// ---------- Security Headers ----------

app.Use(async (context, next) =>
{
    context.Response.Headers.Append(
        "X-Content-Type-Options",
        "nosniff");

    context.Response.Headers.Append(
        "X-Frame-Options",
        "DENY");

    context.Response.Headers.Append(
        "Referrer-Policy",
        "no-referrer");

    await next();
});

// ---------- Swagger ----------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ---------- Middleware Pipeline ----------

app.UseHttpsRedirection();

app.UseCors("FlutterClients");

// UseAuthentication() must run before UseRateLimiter(): the rate limiter partitions
// by authenticated user id (see PartitionKey above), which only exists on
// HttpContext.User once the JWT middleware has run.
app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

// ---------- Health ----------
app.MapMethods("/health", new[] { "GET", "HEAD" }, () =>
    Results.Ok(new { status = "ok" }));
// ---------- Database Migration ----------

// ---------- Database Migration + Development reference-data provisioning ----------
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // Gap 1 — a fresh database intentionally has zero reference rows (PR #66 removed
    // the static seed; the AI track's curated CSVs are the source of truth). In the
    // Development environment only, provision them at startup from
    // AI/01-Dataset/curated-data: additive, idempotent, never deletes rows, and cheap
    // (small fixed CSVs, insert-if-missing only — not an expensive import on every
    // startup, and never run outside Development). Testing keeps its own fixture
    // seeding; Staging/Production are never seeded by the application.
    if (app.Environment.IsDevelopment())
    {
        await SeedData.EnsureCuratedDatasetAsync(
            db,
            app.Configuration,
            app.Environment,
            scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("SeedData"));
    }
}

app.Run();

public partial class Program { }