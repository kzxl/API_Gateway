using APIGateway.Core.Constants;
using APIGateway.Core.Interfaces;
using APIGateway.Data;
using APIGateway.Features.Auth;
using APIGateway.Features.Clustering;
using APIGateway.Features.Monitoring;
using APIGateway.Features.Routing;
using APIGateway.Infrastructure.Middleware;
using APIGateway.Middleware;
using APIGateway.Models;
using APIGateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// ==============================
// 1. Database (Infrastructure)
// ==============================
var connectionString = builder.Configuration.GetConnectionString("GatewayDb")
                      ?? "Data Source=gateway.db";
builder.Services.AddDbContext<GatewayDbContext>(opt =>
    opt.UseSqlite(connectionString));

// ==============================
// 2. Feature Services (UArch: Contract-First DI)
// ==============================
builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<IClusterService, ClusterService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILogService, LogService>();

// Legacy repository (used by proxy provider)
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddSingleton<DbProxyConfigProvider>();
builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<DbProxyConfigProvider>());

// ==============================
// 3. JWT Authentication (env var > config > default)
// ==============================
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? builder.Configuration["Jwt:Secret"]
    ?? "GatewaySecretKey-Change-This-In-Production-Min32Chars!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "APIGateway",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "GatewayClients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// ==============================
// 4. Reverse Proxy + Controllers
// ==============================
builder.Services.AddReverseProxy();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==============================
// 5. CORS
// ==============================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAdminUI", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ==============================
// 6. Seed database
// ==============================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
    db.Database.EnsureCreated();

    if (!db.Users.Any())
    {
        db.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = Roles.Admin,
            IsActive = true
        });
    }

    if (!db.Clusters.Any())
    {
        db.Clusters.Add(new Cluster
        {
            ClusterId = "default-cluster",
            DestinationsJson = "[{\"id\":\"dest-1\",\"address\":\"http://localhost:5001\",\"health\":\"Active\"},{\"id\":\"dest-2\",\"address\":\"http://localhost:5002\",\"health\":\"Standby\"}]",
            EnableHealthCheck = true,
            HealthCheckPath = Defaults.HealthCheckPath,
            HealthCheckIntervalSeconds = Defaults.HealthCheckIntervalSeconds,
            HealthCheckTimeoutSeconds = Defaults.HealthCheckTimeoutSeconds,
            LoadBalancingPolicy = LoadBalancing.RoundRobin
        });
    }

    if (!db.Routes.Any())
    {
        db.Routes.Add(new APIGateway.Models.Route
        {
            RouteId = "default-route",
            ClusterId = "default-cluster",
            MatchPath = "/{**catch-all}",
            RateLimitPerSecond = 100
        });
    }

    db.SaveChanges();
}

// ==============================
// 7. Middleware pipeline (UArch #7: Middleware = Gravity)
// ==============================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global error handler (outermost — catches everything)
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("AllowAdminUI");

// Metrics tracking
app.UseMiddleware<MetricsMiddleware>();

// Gateway protection: IP filter → Rate limit → Circuit breaker → Logging
app.UseMiddleware<GatewayProtectionMiddleware>();

// JWT auth for proxy traffic
app.UseAuthentication();
app.UseAuthorization();

// API key auth for admin endpoints only
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapControllers();
app.MapReverseProxy();
app.Run();
