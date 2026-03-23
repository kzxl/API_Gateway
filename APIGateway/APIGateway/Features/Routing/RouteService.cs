using APIGateway.Core.Interfaces;
using APIGateway.Core.Interfaces.DTOs;
using APIGateway.Data;
using Microsoft.EntityFrameworkCore;

namespace APIGateway.Features.Routing;

public class RouteService : IRouteService
{
    private readonly GatewayDbContext _db;

    public RouteService(GatewayDbContext db) => _db = db;

    public async Task<List<RouteDto>> GetAllAsync()
    {
        return await _db.Routes.Select(r => new RouteDto(
            r.Id, r.RouteId, r.MatchPath, r.Methods, r.ClusterId,
            r.RateLimitPerSecond, r.CircuitBreakerThreshold, r.CircuitBreakerDurationSeconds,
            r.IpWhitelist, r.IpBlacklist, r.CacheTtlSeconds, r.TransformsJson
        )).ToListAsync();
    }

    public async Task<RouteDto?> GetByIdAsync(int id)
    {
        var r = await _db.Routes.FindAsync(id);
        if (r == null) return null;
        return new RouteDto(r.Id, r.RouteId, r.MatchPath, r.Methods, r.ClusterId,
            r.RateLimitPerSecond, r.CircuitBreakerThreshold, r.CircuitBreakerDurationSeconds,
            r.IpWhitelist, r.IpBlacklist, r.CacheTtlSeconds, r.TransformsJson);
    }

    public async Task<RouteDto> CreateAsync(CreateRouteDto dto)
    {
        var route = new Models.Route
        {
            RouteId = dto.RouteId,
            MatchPath = dto.MatchPath,
            Methods = dto.Methods,
            ClusterId = dto.ClusterId,
            RateLimitPerSecond = dto.RateLimitPerSecond,
            CircuitBreakerThreshold = dto.CircuitBreakerThreshold,
            CircuitBreakerDurationSeconds = dto.CircuitBreakerDurationSeconds,
            IpWhitelist = dto.IpWhitelist,
            IpBlacklist = dto.IpBlacklist,
            CacheTtlSeconds = dto.CacheTtlSeconds,
            TransformsJson = dto.TransformsJson
        };
        _db.Routes.Add(route);
        await _db.SaveChangesAsync();
        return new RouteDto(route.Id, route.RouteId, route.MatchPath, route.Methods, route.ClusterId,
            route.RateLimitPerSecond, route.CircuitBreakerThreshold, route.CircuitBreakerDurationSeconds,
            route.IpWhitelist, route.IpBlacklist, route.CacheTtlSeconds, route.TransformsJson);
    }

    public async Task<RouteDto?> UpdateAsync(int id, CreateRouteDto dto)
    {
        var route = await _db.Routes.FindAsync(id);
        if (route == null) return null;

        route.RouteId = dto.RouteId;
        route.MatchPath = dto.MatchPath;
        route.Methods = dto.Methods;
        route.ClusterId = dto.ClusterId;
        route.RateLimitPerSecond = dto.RateLimitPerSecond;
        route.CircuitBreakerThreshold = dto.CircuitBreakerThreshold;
        route.CircuitBreakerDurationSeconds = dto.CircuitBreakerDurationSeconds;
        route.IpWhitelist = dto.IpWhitelist;
        route.IpBlacklist = dto.IpBlacklist;
        route.CacheTtlSeconds = dto.CacheTtlSeconds;
        route.TransformsJson = dto.TransformsJson;

        await _db.SaveChangesAsync();
        return new RouteDto(route.Id, route.RouteId, route.MatchPath, route.Methods, route.ClusterId,
            route.RateLimitPerSecond, route.CircuitBreakerThreshold, route.CircuitBreakerDurationSeconds,
            route.IpWhitelist, route.IpBlacklist, route.CacheTtlSeconds, route.TransformsJson);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var route = await _db.Routes.FindAsync(id);
        if (route == null) return false;
        _db.Routes.Remove(route);
        await _db.SaveChangesAsync();
        return true;
    }
}
