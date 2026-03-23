using APIGateway.Core.Interfaces;
using APIGateway.Core.Interfaces.DTOs;
using APIGateway.Data;
using Microsoft.EntityFrameworkCore;

namespace APIGateway.Features.Clustering;

public class ClusterService : IClusterService
{
    private readonly GatewayDbContext _db;
    private static readonly HttpClient _goFlowClient = new() { BaseAddress = new Uri("http://127.0.0.1:50051") };

    public ClusterService(GatewayDbContext db) => _db = db;

    private void BumpRoutesVersion()
    {
        _ = Task.Run(async () =>
        {
            try { await _goFlowClient.PostAsync("/cache/routes_version/bump", null); } catch { }
        });
    }

    public async Task<List<ClusterDto>> GetAllAsync()
    {
        return await _db.Clusters.Select(c => new ClusterDto(
            c.Id, c.ClusterId, c.DestinationsJson,
            c.EnableHealthCheck, c.HealthCheckPath, c.HealthCheckIntervalSeconds, c.HealthCheckTimeoutSeconds,
            c.LoadBalancingPolicy, c.RetryCount, c.RetryDelayMs
        )).ToListAsync();
    }

    public async Task<ClusterDto?> GetByIdAsync(int id)
    {
        var c = await _db.Clusters.FindAsync(id);
        if (c == null) return null;
        return new ClusterDto(c.Id, c.ClusterId, c.DestinationsJson,
            c.EnableHealthCheck, c.HealthCheckPath, c.HealthCheckIntervalSeconds, c.HealthCheckTimeoutSeconds,
            c.LoadBalancingPolicy, c.RetryCount, c.RetryDelayMs);
    }

    public async Task<ClusterDto> CreateAsync(CreateClusterDto dto)
    {
        var cluster = new Models.Cluster
        {
            ClusterId = dto.ClusterId,
            DestinationsJson = dto.DestinationsJson,
            EnableHealthCheck = dto.EnableHealthCheck,
            HealthCheckPath = dto.HealthCheckPath,
            HealthCheckIntervalSeconds = dto.HealthCheckIntervalSeconds,
            HealthCheckTimeoutSeconds = dto.HealthCheckTimeoutSeconds,
            LoadBalancingPolicy = dto.LoadBalancingPolicy,
            RetryCount = dto.RetryCount,
            RetryDelayMs = dto.RetryDelayMs
        };
        _db.Clusters.Add(cluster);
        await _db.SaveChangesAsync();
        BumpRoutesVersion();
        
        return new ClusterDto(cluster.Id, cluster.ClusterId, cluster.DestinationsJson,
            cluster.EnableHealthCheck, cluster.HealthCheckPath, cluster.HealthCheckIntervalSeconds,
            cluster.HealthCheckTimeoutSeconds, cluster.LoadBalancingPolicy, cluster.RetryCount, cluster.RetryDelayMs);
    }

    public async Task<ClusterDto?> UpdateAsync(int id, CreateClusterDto dto)
    {
        var cluster = await _db.Clusters.FindAsync(id);
        if (cluster == null) return null;

        cluster.ClusterId = dto.ClusterId;
        cluster.DestinationsJson = dto.DestinationsJson;
        cluster.EnableHealthCheck = dto.EnableHealthCheck;
        cluster.HealthCheckPath = dto.HealthCheckPath;
        cluster.HealthCheckIntervalSeconds = dto.HealthCheckIntervalSeconds;
        cluster.HealthCheckTimeoutSeconds = dto.HealthCheckTimeoutSeconds;
        cluster.LoadBalancingPolicy = dto.LoadBalancingPolicy;
        cluster.RetryCount = dto.RetryCount;
        cluster.RetryDelayMs = dto.RetryDelayMs;

        await _db.SaveChangesAsync();
        BumpRoutesVersion();
        
        return new ClusterDto(cluster.Id, cluster.ClusterId, cluster.DestinationsJson,
            cluster.EnableHealthCheck, cluster.HealthCheckPath, cluster.HealthCheckIntervalSeconds,
            cluster.HealthCheckTimeoutSeconds, cluster.LoadBalancingPolicy, cluster.RetryCount, cluster.RetryDelayMs);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var cluster = await _db.Clusters.FindAsync(id);
        if (cluster == null) return false;
        _db.Clusters.Remove(cluster);
        await _db.SaveChangesAsync();
        BumpRoutesVersion();
        
        return true;
    }
}
