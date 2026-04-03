namespace APIGateway.Core.Interfaces;

/// <summary>
/// Token management service for JWT refresh tokens and session tracking.
/// UArch: Contract-first DI pattern.
/// </summary>
public interface ITokenService
{
    Task<Models.RefreshToken> CreateRefreshTokenAsync(int userId, string ipAddress);
    Task<Models.RefreshToken?> ValidateRefreshTokenAsync(string token);
    Task<Models.RefreshToken> RotateRefreshTokenAsync(Models.RefreshToken oldToken, string ipAddress);
    Task RevokeRefreshTokenAsync(string token, string ipAddress);
    Task RevokeAllUserTokensAsync(int userId);
    Task<bool> IsAccessTokenBlacklistedAsync(string jti);
    Task BlacklistAccessTokenAsync(string jti, TimeSpan expiration);
    Task<Models.UserSession> CreateSessionAsync(int userId, string jti, string refreshToken, string ipAddress, string userAgent, TimeSpan expiration);
    Task UpdateSessionActivityAsync(string jti);
    Task RevokeSessionAsync(string jti);
}
