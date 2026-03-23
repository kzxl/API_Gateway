using APIGateway.Data;
using APIGateway.Middleware;
using APIGateway.Models;
using APIGateway.Services;
using Microsoft.EntityFrameworkCore;
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
// 3. Reverse Proxy + Controllers
// ==============================
builder.Services.AddReverseProxy();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==============================
// 4. CORS
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
// 5. Ensure database & seed
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
            DestinationsJson = "[{\"id\":\"dest-1\",\"address\":\"http://localhost:5001\"}]"
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
// 6. Middleware pipeline
// ==============================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAdminUI");

// API key auth for admin endpoints
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.UseAuthorization();
app.MapControllers();
app.MapReverseProxy();
app.Run();
