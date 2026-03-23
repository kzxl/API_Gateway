using APIGateway.Data;
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
// 1. Database
// ==============================
var connectionString = builder.Configuration.GetConnectionString("GatewayDb")
                      ?? "Data Source=gateway.db";

builder.Services.AddDbContext<GatewayDbContext>(opt =>
    opt.UseSqlite(connectionString));

// ==============================
// 2. Repository + Proxy Provider
// ==============================
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddSingleton<DbProxyConfigProvider>();
builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<DbProxyConfigProvider>());

// ==============================
// 3. JWT Authentication
// ==============================
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "GatewaySecretKey-Change-This-In-Production-Min32Chars!";
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
// 6. Ensure database & seed
// ==============================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
    db.Database.EnsureCreated();

    if (!db.Clusters.Any())
    {
        db.Clusters.Add(new Cluster
        {
            ClusterId = "default-cluster",
            DestinationsJson = "[{\"id\":\"dest-1\",\"address\":\"http://localhost:5001\",\"health\":\"Active\"},{\"id\":\"dest-2\",\"address\":\"http://localhost:5002\",\"health\":\"Standby\"}]",
            EnableHealthCheck = true,
            HealthCheckPath = "/health",
            HealthCheckIntervalSeconds = 10,
            HealthCheckTimeoutSeconds = 5,
            LoadBalancingPolicy = "RoundRobin"
        });
    }

    if (!db.Routes.Any())
    {
        db.Routes.Add(new APIGateway.Models.Route
        {
            RouteId = "default-route",
            ClusterId = "default-cluster",
            MatchPath = "/{**catch-all}"
        });
    }

    db.SaveChanges();
}

// ==============================
// 7. Middleware pipeline
// ==============================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAdminUI");

// Metrics tracking (before auth, so we count all requests)
app.UseMiddleware<MetricsMiddleware>();

// JWT auth for proxy traffic
app.UseAuthentication();
app.UseAuthorization();

// API key auth for admin endpoints only
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapControllers();
app.MapReverseProxy();
app.Run();
