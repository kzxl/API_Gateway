using APIGateway.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APIGateway.Controllers;

[ApiController]
[Route("admin/logs")]
public class AdminLogsController : ControllerBase
{
    private readonly GatewayDbContext _db;

    public AdminLogsController(GatewayDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? routeId = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] string? method = null)
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
            .ToListAsync();

        return Ok(new { total, page, pageSize, logs });
    }

    [HttpDelete]
    public async Task<IActionResult> ClearLogs()
    {
        await _db.RequestLogs.ExecuteDeleteAsync();
        return Ok(new { message = "Logs cleared" });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var total = await _db.RequestLogs.CountAsync();
        var last24h = await _db.RequestLogs
            .Where(l => l.Timestamp > DateTime.UtcNow.AddHours(-24))
            .CountAsync();

        var byStatus = await _db.RequestLogs
            .GroupBy(l => l.StatusCode / 100)
            .Select(g => new { StatusGroup = g.Key + "xx", Count = g.Count() })
            .ToListAsync();

        var topRoutes = await _db.RequestLogs
            .GroupBy(l => l.RouteId)
            .Select(g => new { RouteId = g.Key, Count = g.Count(), AvgLatency = g.Average(l => l.LatencyMs) })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        return Ok(new { total, last24h, byStatus, topRoutes });
    }
}
