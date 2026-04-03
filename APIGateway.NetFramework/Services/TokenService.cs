using System;
using System.Collections.Concurrent;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using APIGateway.NetFramework.Data;
using APIGateway.NetFramework.Models;

namespace APIGateway.NetFramework.Services
{
    public class TokenService : ITokenService
    {
        private readonly GatewayDbContext _db;

        // Blacklisted JTIs (in-memory cache)
        private static readonly ConcurrentDictionary<string, DateTime> _blacklistedJtis = new ConcurrentDictionary<string, DateTime>();

        public TokenService(GatewayDbContext db)
        {
            _db = db;
        }

        public async Task<RefreshToken> CreateRefreshTokenAsync(int userId, string ipAddress)
        {
            var token = new RefreshToken
            {
                UserId = userId,
                Token = GenerateRefreshToken(),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedByIp = ipAddress
            };

            _db.RefreshTokens.Add(token);
            await _db.SaveChangesAsync();

            return token;
        }

        public async Task<RefreshToken> ValidateRefreshTokenAsync(string token)
        {
            var refreshToken = await _db.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == token);

            if (refreshToken == null || !refreshToken.IsActive)
            {
                return null;
            }

            return refreshToken;
        }

        public async Task<RefreshToken> RotateRefreshTokenAsync(RefreshToken oldToken, string ipAddress)
        {
            // Revoke old token
            oldToken.RevokedAt = DateTime.UtcNow;
            oldToken.RevokedByIp = ipAddress;

            // Create new token
            var newToken = new RefreshToken
            {
                UserId = oldToken.UserId,
                Token = GenerateRefreshToken(),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedByIp = ipAddress
            };

            oldToken.ReplacedByToken = newToken.Token;

            _db.RefreshTokens.Add(newToken);
            await _db.SaveChangesAsync();

            return newToken;
        }

        public async Task RevokeRefreshTokenAsync(string token, string ipAddress)
        {
            var refreshToken = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token);
            if (refreshToken == null || refreshToken.IsRevoked) return;

            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            await _db.SaveChangesAsync();
        }

        public Task<bool> IsAccessTokenBlacklistedAsync(string jti)
        {
            return Task.FromResult(_blacklistedJtis.ContainsKey(jti));
        }

        public Task BlacklistAccessTokenAsync(string jti, DateTime expiresAt)
        {
            _blacklistedJtis.TryAdd(jti, expiresAt);
            return Task.CompletedTask;
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

        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes);
        }
    }
}
