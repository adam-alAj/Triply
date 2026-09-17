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


var builder = WebApplication.CreateBuilder(args);

// ---------- Configuration ----------
// Connection string + JWT key + Gemini key all come from configuration/env vars,
// never hardcoded. See .env.example / appsettings.Example.json.

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["ConnectionStrings__Default"]
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured.");

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
    .AddEntityFrameworkStores<ApplicationDbContext>();

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
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit =
            builder.Environment.IsEnvironment("Testing") ? 100 : 10;

        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
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

// ---------- AI-Orchestration (Gemini) — Architecture §4 ADR-01 ----------

builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

builder.Services.AddHttpClient<IGeminiClient, GeminiClient>();
builder.Services.AddScoped<IItineraryPromptBuilder, ItineraryPromptBuilder>();
builder.Services.AddScoped<IItineraryValidator, ItineraryValidationService>();
builder.Services.AddScoped<IAiOrchestrationService, AiOrchestrationService>();

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
app.UseRateLimiter();
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// ---------- Health ----------

app.MapGet("/health", () =>
    Results.Ok(new { status = "ok" }));

// ---------- Database Migration ----------
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.Run();

public partial class Program { }