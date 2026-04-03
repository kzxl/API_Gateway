using System;

namespace APIGateway.NetFramework.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockedUntil { get; set; }
        public bool IsLocked { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; }
        public UserDto User { get; set; }
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; }
    }

    public class RefreshTokenResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
    }

    public class CreateRouteDto
    {
        public string RouteId { get; set; }
        public string ClusterId { get; set; }
        public string MatchPath { get; set; }
        public int RateLimitPerSecond { get; set; }
    }

    public class UpdateRouteDto
    {
        public string ClusterId { get; set; }
        public string MatchPath { get; set; }
        public int RateLimitPerSecond { get; set; }
        public bool IsActive { get; set; }
    }
}
