using APIGateway.Core.Interfaces;
using APIGateway.Core.Interfaces.DTOs;
using APIGateway.Data;
using Microsoft.EntityFrameworkCore;

namespace APIGateway.Features.Monitoring;

public class LogService : ILogService
{
    private readonly GatewayDbContext _db;

    public LogService(GatewayDbContext db) => _db = db;

    public async Task<LogPageDto> GetLogsAsync(int page, int pageSize, string? routeId, int? statusCode, string? method)
    {
        var query = _db.RequestLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(routeId))
            query = query.Where(l => l.RouteId == routeId);
        if (statusCode.HasValue)
            query = query.Where(l => l.StatusCode == statusCode.Value);
        if (!string.IsNullOrWhiteSpace(method))
            query = query.Where(l => l.Method == method.ToUpper());

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LogEntryDto(l.Id, l.Timestamp, l.Method, l.Path, l.StatusCode, l.LatencyMs, l.ClientIp, l.RouteId, l.UserAgent))
            .ToListAsync();

        return new LogPageDto(total, page, pageSize, logs);
    }

    public async Task<LogStatsDto> GetStatsAsync()
    {
        var total = await _db.RequestLogs.CountAsync();
        var last24h = await _db.RequestLogs
            .Where(l => l.Timestamp > DateTime.UtcNow.AddHours(-24)).CountAsync();

        var byStatus = await _db.RequestLogs
            .GroupBy(l => l.StatusCode / 100)
            .Select(g => new StatusGroupDto(g.Key + "xx", g.Count()))
            .ToListAsync();

        var topRoutes = await _db.RequestLogs
            .GroupBy(l => l.RouteId)
            .Select(g => new TopRouteDto(g.Key, g.Count(), g.Average(l => l.LatencyMs)))
            .OrderByDescending(x => x.Count)
            .Take(10).ToListAsync();

        return new LogStatsDto(total, last24h, byStatus, topRoutes);
    }

    public async Task ClearAsync()
    {
        await _db.RequestLogs.ExecuteDeleteAsync();
    }
}
