using APIGateway.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace APIGateway.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IUserService _userService;

    public AuthController(IConfiguration config, IUserService userService)
    {
        _config = config;
        _userService = userService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required" });

        var user = await _userService.ValidateCredentialsAsync(request.Username, request.Password);
        if (user == null)
            return Unauthorized(new { error = "Invalid credentials" });

        var token = GenerateJwtToken(user.Username, user.Role);
        return Ok(new
        {
            token,
            expiresIn = int.Parse(_config["Jwt:ExpirationMinutes"] ?? "60") * 60,
            tokenType = "Bearer",
            user = new { user.Username, user.Role }
        });
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

    private string GenerateJwtToken(string username, string role)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        var expMinutes = int.Parse(_config["Jwt:ExpirationMinutes"] ?? "60");

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public record LoginRequest(string Username, string Password);
    public record ValidateRequest(string Token);
}
