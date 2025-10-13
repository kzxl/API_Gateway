using APIGateway.Data;
using APIGateway.Models;
using APIGateway.Services;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;
using Yarp.ReverseProxy.Configuration;


var builder = WebApplication.CreateBuilder(args);

// ==============================
// 1️  Configure Database
// ==============================
builder.Services.AddDbContext<GatewayDbContext>(opt =>
    opt.UseSqlite("Data Source=gateway.db"));

// ==============================
// 2️  Repository + Proxy Provider
// ==============================
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddSingleton<DbProxyConfigProvider>();
builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<DbProxyConfigProvider>());

// ==============================
// 3️  Reverse Proxy + Controllers
// ==============================
builder.Services.AddReverseProxy();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ==============================
// 4️  Ensure database and seed default data
// ==============================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
    db.Database.EnsureCreated();

    // Dữ liệu mẫu nếu chưa có
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
// 5️  Middleware pipeline
// ==============================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Nếu backend của bạn không có HTTPS hoặc là localhost
// bạn có thể comment dòng dưới để tránh lỗi HTTPS connect
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();
app.MapReverseProxy();
app.Run();
