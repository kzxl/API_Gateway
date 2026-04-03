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
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

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
                "http://192.168.19.79:8888",
                "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddMemoryCache();

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

    // Seed default permissions
    if (!db.Permissions.Any())
    {
        var permissions = new[]
        {
            new Permission { Name = "routes.read", Resource = "routes", Action = "read", Description = "View routes" },
            new Permission { Name = "routes.write", Resource = "routes", Action = "write", Description = "Create/update routes" },
            new Permission { Name = "routes.delete", Resource = "routes", Action = "delete", Description = "Delete routes" },
            new Permission { Name = "clusters.read", Resource = "clusters", Action = "read", Description = "View clusters" },
            new Permission { Name = "clusters.write", Resource = "clusters", Action = "write", Description = "Create/update clusters" },
            new Permission { Name = "clusters.delete", Resource = "clusters", Action = "delete", Description = "Delete clusters" },
            new Permission { Name = "users.read", Resource = "users", Action = "read", Description = "View users" },
            new Permission { Name = "users.write", Resource = "users", Action = "write", Description = "Create/update users" },
            new Permission { Name = "users.delete", Resource = "users", Action = "delete", Description = "Delete users" },
            new Permission { Name = "permissions.read", Resource = "permissions", Action = "read", Description = "View permissions" },
            new Permission { Name = "permissions.write", Resource = "permissions", Action = "write", Description = "Manage permissions" },
            new Permission { Name = "logs.read", Resource = "logs", Action = "read", Description = "View logs" },
            new Permission { Name = "logs.delete", Resource = "logs", Action = "delete", Description = "Delete logs" },
            new Permission { Name = "metrics.read", Resource = "metrics", Action = "read", Description = "View metrics" }
        };
        db.Permissions.AddRange(permissions);
        db.SaveChanges();

        // Grant all permissions to Admin role
        foreach (var permission in permissions)
        {
            db.RolePermissions.Add(new RolePermission
            {
                Role = Roles.Admin,
                PermissionId = permission.Id
            });
        }

        // Grant read-only permissions to User role
        var readPermissions = permissions.Where(p => p.Action == "read");
        foreach (var permission in readPermissions)
        {
            db.RolePermissions.Add(new RolePermission
            {
                Role = Roles.User,
                PermissionId = permission.Id
            });
        }
    }

    if (!db.Clusters.Any())
    {
        db.Clusters.Add(new Cluster
        {
            ClusterId = "test-cluster",
            DestinationsJson = "[{\"id\":\"test-backend\",\"address\":\"http://localhost:5001\",\"health\":\"Active\"}]",
            EnableHealthCheck = false,
            LoadBalancingPolicy = LoadBalancing.RoundRobin
        });

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
            RouteId = "test-route",
            ClusterId = "test-cluster",
            MatchPath = "/test/{**catch-all}",
            RateLimitPerSecond = 0  // No rate limit for load testing
        });

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

// Enable WebSocket support
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
    ReceiveBufferSize = 4096
});

// Global error handler (outermost — catches everything)
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors("AllowAdminUI");

// Metrics tracking
app.UseMiddleware<MetricsMiddleware>();

// Throughput control (global rate limiting)
app.UseMiddleware<ThroughputControlMiddleware>();

// Response caching (before gateway protection for max performance)
app.UseMiddleware<ResponseCachingMiddleware>();

// Compression (after caching, before expensive operations)
app.UseMiddleware<CompressionMiddleware>();

// Request/Response transformation
app.UseMiddleware<RequestTransformMiddleware>();

// Gateway protection: IP filter → Rate limit → Circuit breaker → Logging
app.UseMiddleware<GatewayProtectionMiddleware>();

// Request retry with exponential backoff
app.UseMiddleware<RequestRetryMiddleware>();

// JWT auth for proxy traffic
app.UseAuthentication();

// JWT validation with blacklist check (must be after UseAuthentication)
app.UseMiddleware<JwtValidationMiddleware>();

app.UseAuthorization();

// API key auth for admin endpoints only
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapControllers();
app.MapReverseProxy();
app.Run();
