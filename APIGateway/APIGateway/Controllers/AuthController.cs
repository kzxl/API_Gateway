using APIGateway.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace APIGateway.Controllers;

/// <summary>
/// Enhanced Authentication Controller with Refresh Token support.
/// UArch: Thin adapter over services, zero business logic.
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IUserService _userService;
    private readonly ITokenService _tokenService;

    public AuthController(IConfiguration config, IUserService userService, ITokenService tokenService)
    {
        _config = config;
        _userService = userService;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required" });

        // Get user to check lockout status
        var user = await _userService.GetByUsernameAsync(request.Username);

        // Check if account is locked
        if (user != null && user.IsLocked)
        {
            var remainingMinutes = (user.LockedUntil!.Value - DateTime.UtcNow).TotalMinutes;
            return Unauthorized(new
            {
                error = "Account is locked",
                code = "ACCOUNT_LOCKED",
                lockedUntil = user.LockedUntil,
                remainingMinutes = Math.Ceiling(remainingMinutes)
            });
        }

        // Validate credentials
        var validatedUser = await _userService.ValidateCredentialsAsync(request.Username, request.Password);

        if (validatedUser == null)
        {
            // Increment failed login attempts
            if (user != null)
            {
                await _userService.IncrementFailedLoginAsync(user.Id);

                // Check if account should be locked
                if (user.FailedLoginAttempts + 1 >= 5)
                {
                    await _userService.LockAccountAsync(user.Id, TimeSpan.FromMinutes(30));
                    return Unauthorized(new
                    {
                        error = "Too many failed attempts. Account locked for 30 minutes.",
                        code = "ACCOUNT_LOCKED",
                        attemptsRemaining = 0
                    });
                }

                return Unauthorized(new
                {
                    error = "Invalid credentials",
                    code = "INVALID_CREDENTIALS",
                    attemptsRemaining = 5 - (user.FailedLoginAttempts + 1)
                });
            }

            return Unauthorized(new { error = "Invalid credentials" });
        }

        // Reset failed login attempts on successful login
        await _userService.ResetFailedLoginAsync(validatedUser.Id);

        // Generate short-lived access token (15 minutes)
        var (accessToken, jti) = GenerateAccessToken(validatedUser.Id, validatedUser.Username, validatedUser.Role);

        // Generate long-lived refresh token (7 days)
        var refreshToken = await _tokenService.CreateRefreshTokenAsync(validatedUser.Id, GetClientIp());

        // Create session for tracking
        await _tokenService.CreateSessionAsync(
            validatedUser.Id,
            jti,
            refreshToken.Token,
            GetClientIp(),
            GetUserAgent(),
            TimeSpan.FromMinutes(15)
        );

        return Ok(new
        {
            accessToken,
            refreshToken = refreshToken.Token,
            expiresIn = 900, // 15 minutes in seconds
            tokenType = "Bearer",
            user = new { validatedUser.Id, validatedUser.Username, validatedUser.Role }
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "Refresh token is required" });

        var refreshToken = await _tokenService.ValidateRefreshTokenAsync(request.RefreshToken);
        if (refreshToken == null)
            return Unauthorized(new { error = "Invalid or expired refresh token" });

        var user = await _userService.GetByIdAsync(refreshToken.UserId);
        if (user == null)
            return Unauthorized(new { error = "User not found" });

        // Rotate refresh token (security best practice)
        var newRefreshToken = await _tokenService.RotateRefreshTokenAsync(refreshToken, GetClientIp());

        // Generate new access token
        var (newAccessToken, jti) = GenerateAccessToken(user.Id, user.Username, user.Role);

        // Create new session
        await _tokenService.CreateSessionAsync(
            user.Id,
            jti,
            newRefreshToken.Token,
            GetClientIp(),
            GetUserAgent(),
            TimeSpan.FromMinutes(15)
        );

        return Ok(new
        {
            accessToken = newAccessToken,
            refreshToken = newRefreshToken.Token,
            expiresIn = 900
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        // Revoke refresh token
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken, GetClientIp());
        }

        // Blacklist current access token
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (!string.IsNullOrEmpty(jti))
        {
            await _tokenService.BlacklistAccessTokenAsync(jti, TimeSpan.FromMinutes(15));
            await _tokenService.RevokeSessionAsync(jti);
        }

        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("validate")]
    public IActionResult Validate([FromBody] ValidateRequest request)
    {
        try
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(request.Token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidAudience = _config["Jwt:Audience"],
                IssuerSigningKey = key
            }, out _);

            return Ok(new
            {
                valid = true,
                claims = principal.Claims.Select(c => new { c.Type, c.Value })
            });
        }
        catch (Exception ex)
        {
            return Ok(new { valid = false, error = ex.Message });
        }
    }

    private (string Token, string Jti) GenerateAccessToken(int userId, string username, string role)
    {
        var jti = Guid.NewGuid().ToString();
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), // Short-lived for security
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), jti);
    }

    private string GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string GetUserAgent() =>
        HttpContext.Request.Headers.UserAgent.FirstOrDefault() ?? "unknown";

    public record LoginRequest(string Username, string Password);
    public record RefreshRequest(string RefreshToken);
    public record LogoutRequest(string RefreshToken);
    public record ValidateRequest(string Token);
}
