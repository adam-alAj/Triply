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
using Triply.Api.Modules.Auth;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configuration (Task: Environment & Config Conventions) ----------
// Connection string + JWT key + Gemini key all come from configuration/env vars,
// never hardcoded. See .env.example / appsettings.Example.json.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["ConnectionStrings__Default"]
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

// ---------- EF Core + SQL Server (Task 1) ----------
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseSqlServer(connectionString));

// ---------- Identity (Task 3) ----------
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

// ---------- JWT Auth (Task 5/6) ----------
var jwtKey = builder.Configuration["Jwt:Key"] ?? "DEV-ONLY-CHANGE-ME";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Triply",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TriplyClients",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

// ---------- Ownership authorization (Task 6) ----------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TripOwner", policy =>
        policy.Requirements.Add(new TripOwnerRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, TripOwnerHandler>();

// ---------- FluentValidation (Task 7) ----------
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ---------- Rate limiting + CORS (Task 8) ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;
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
var flutterOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" }; // placeholder for local Flutter Web dev
builder.Services.AddCors(options =>
{
    options.AddPolicy("FlutterClients", policy =>
        policy.WithOrigins(flutterOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------- Centralized error handling (Task 7) ----------
app.UseMiddleware<ExceptionHandlingMiddleware>();

// ---------- Security headers (Task 8) ----------
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("FlutterClients");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () =>
    Results.Ok(new { status = "ok" }));
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.Run();

public partial class Program { }

