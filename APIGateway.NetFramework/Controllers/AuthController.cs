using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using APIGateway.NetFramework.Models;
using APIGateway.NetFramework.Services;
using APIGateway.NetFramework.Infrastructure;

namespace APIGateway.NetFramework.Controllers
{
    /// <summary>
    /// Authentication controller for .NET Framework 4.8.
    /// UArch: Thin adapter over services.
    /// </summary>
    [RoutePrefix("auth")]
    public class AuthController : ApiController
    {
        private readonly IUserService _userService;
        private readonly ITokenService _tokenService;

        public AuthController(IUserService userService, ITokenService tokenService)
        {
            _userService = userService;
            _tokenService = tokenService;
        }

        [HttpPost]
        [Route("login")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Username and password are required");
            }

            // Get user to check lockout
            var user = await _userService.GetByUsernameAsync(request.Username);

            // Check if account is locked
            if (user != null && user.IsLocked)
            {
                var remainingMinutes = (user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes;
                return Content(HttpStatusCode.Unauthorized, new
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
                        return Content(HttpStatusCode.Unauthorized, new
                        {
                            error = "Too many failed attempts. Account locked for 30 minutes.",
                            code = "ACCOUNT_LOCKED",
                            attemptsRemaining = 0
                        });
                    }

                    return Content(HttpStatusCode.Unauthorized, new
                    {
                        error = "Invalid credentials",
                        code = "INVALID_CREDENTIALS",
                        attemptsRemaining = 5 - (user.FailedLoginAttempts + 1)
                    });
                }

                return Unauthorized();
            }

            // Reset failed login attempts
            await _userService.ResetFailedLoginAsync(validatedUser.Id);

            // Generate tokens
            var accessToken = GenerateAccessToken(validatedUser);
            var refreshToken = await _tokenService.CreateRefreshTokenAsync(validatedUser.Id, GetClientIp());

            return Ok(new
            {
                accessToken,
                refreshToken = refreshToken.Token,
                expiresIn = 900,
                tokenType = "Bearer",
                user = new { validatedUser.Id, validatedUser.Username, validatedUser.Role }
            });
        }

        [HttpPost]
        [Route("refresh")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> RefreshToken([FromBody] RefreshRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest("Refresh token is required");
            }

            var refreshToken = await _tokenService.ValidateRefreshTokenAsync(request.RefreshToken);
            if (refreshToken == null)
            {
                return Unauthorized();
            }

            var user = await _userService.GetByIdAsync(refreshToken.UserId);
            if (user == null)
            {
                return Unauthorized();
            }

            // Rotate refresh token
            var newRefreshToken = await _tokenService.RotateRefreshTokenAsync(refreshToken, GetClientIp());
            var newAccessToken = GenerateAccessToken(user);

            return Ok(new
            {
                accessToken = newAccessToken,
                refreshToken = newRefreshToken.Token,
                expiresIn = 900
            });
        }

        [HttpPost]
        [Route("logout")]
        [Authorize]
        public async Task<IHttpActionResult> Logout([FromBody] LogoutRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken, GetClientIp());
            }

            return Ok(new { message = "Logged out successfully" });
        }

        private string GenerateAccessToken(UserDto user)
        {
            // JWT generation logic here
            // Use System.IdentityModel.Tokens.Jwt
            return "jwt_token_here";
        }

        private string GetClientIp()
        {
            if (Request.Properties.ContainsKey("MS_HttpContext"))
            {
                var context = Request.Properties["MS_HttpContext"] as System.Web.HttpContextWrapper;
                if (context != null)
                {
                    return context.Request.UserHostAddress;
                }
            }
            return "unknown";
        }

        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public class RefreshRequest
        {
            public string RefreshToken { get; set; }
        }

        public class LogoutRequest
        {
            public string RefreshToken { get; set; }
        }
    }
}
