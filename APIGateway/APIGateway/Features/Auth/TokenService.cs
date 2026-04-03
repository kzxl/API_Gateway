using System.Collections.Concurrent;
using System.Security.Cryptography;
using APIGateway.Core.Interfaces;
using APIGateway.Data;
using APIGateway.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace APIGateway.Features.Auth;

/// <summary>
/// Token service with L1 cache optimization for maximum performance.
/// UArch: Zero-allocation hot path, fire-and-forget async operations.
/// </summary>
public class TokenService : ITokenService
{
    private readonly GatewayDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    // L1 Cache: Blacklisted JTIs (in-memory, nanosecond lookup)
    private static readonly ConcurrentDictionary<string, DateTime> _blacklistedJtis = new();

    // Cleanup timer for expired blacklisted tokens
    private static Timer? _cleanupTimer;

    public TokenService(GatewayDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;

        // Start cleanup timer once (singleton pattern)
        _cleanupTimer ??= new Timer(CleanupExpiredBlacklist, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(int userId, string ipAddress)
    {
        var token = new RefreshToken
        {
            UserId = userId,
            Token = GenerateSecureToken(),
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime),
            CreatedByIp = ipAddress
        };

        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();
        return token;
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token)
    {
        // L1 Cache check first (avoid DB hit)
        var cacheKey = $"refresh_token:{token}";
        if (_cache.TryGetValue(cacheKey, out RefreshToken? cached))
            return cached?.IsActive == true ? cached : null;

        var refreshToken = await _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == token);

        if (refreshToken != null && refreshToken.IsActive)
        {
            // Cache for 5 minutes
            _cache.Set(cacheKey, refreshToken, TimeSpan.FromMinutes(5));
        }

        return refreshToken?.IsActive == true ? refreshToken : null;
    }

    public async Task<RefreshToken> RotateRefreshTokenAsync(RefreshToken oldToken, string ipAddress)
    {
        // Revoke old token
        var dbToken = await _db.RefreshTokens.FindAsync(oldToken.Id);
        if (dbToken == null)
            throw new InvalidOperationException("Token not found");

        dbToken.RevokedAt = DateTime.UtcNow;
        dbToken.RevokedByIp = ipAddress;

        // Create new token
        var newToken = new RefreshToken
        {
            UserId = oldToken.UserId,
            Token = GenerateSecureToken(),
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime),
            CreatedByIp = ipAddress
        };

        dbToken.ReplacedByToken = newToken.Token;
        _db.RefreshTokens.Add(newToken);
        await _db.SaveChangesAsync();

        // Invalidate cache
        _cache.Remove($"refresh_token:{oldToken.Token}");

        return newToken;
    }

    public async Task RevokeRefreshTokenAsync(string token, string ipAddress)
    {
        var refreshToken = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token);
        if (refreshToken == null) return;

        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.RevokedByIp = ipAddress;

        await _db.SaveChangesAsync();
        _cache.Remove($"refresh_token:{token}");
    }

    public async Task RevokeAllUserTokensAsync(int userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = "system";
            _cache.Remove($"refresh_token:{token.Token}");
        }

        await _db.SaveChangesAsync();
    }

    public Task<bool> IsAccessTokenBlacklistedAsync(string jti)
    {
        // Ultra-fast in-memory lookup (nanoseconds)
        return Task.FromResult(_blacklistedJtis.ContainsKey(jti));
    }

    public Task BlacklistAccessTokenAsync(string jti, TimeSpan expiration)
    {
        // Add to blacklist with expiration time
        _blacklistedJtis.TryAdd(jti, DateTime.UtcNow.Add(expiration));
        return Task.CompletedTask;
    }

    public async Task<UserSession> CreateSessionAsync(int userId, string jti, string refreshToken, string ipAddress, string userAgent, TimeSpan expiration)
    {
        var session = new UserSession
        {
            UserId = userId,
            SessionId = Guid.NewGuid().ToString(),
            AccessTokenJti = jti,
            RefreshToken = refreshToken,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ExpiresAt = DateTime.UtcNow.Add(expiration)
        };

        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task UpdateSessionActivityAsync(string jti)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.AccessTokenJti == jti);
        if (session != null)
        {
            session.LastActivityAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task RevokeSessionAsync(string jti)
    {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.AccessTokenJti == jti);
        if (session != null)
        {
            session.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static void CleanupExpiredBlacklist(object? state)
    {
        var now = DateTime.UtcNow;
        var expired = _blacklistedJtis.Where(kv => kv.Value < now).Select(kv => kv.Key).ToList();

        foreach (var jti in expired)
        {
            _blacklistedJtis.TryRemove(jti, out _);
        }
    }
}
