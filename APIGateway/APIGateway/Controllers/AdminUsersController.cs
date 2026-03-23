using APIGateway.Data;
using APIGateway.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APIGateway.Controllers;

[ApiController]
[Route("admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly GatewayDbContext _db;

    public AdminUsersController(GatewayDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .Select(u => new { u.Id, u.Username, u.Role, u.IsActive, u.CreatedAt })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Username and password are required" });

        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict(new { error = "Username already exists" });

        var user = new User
        {
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = req.Role ?? "User",
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.Username, user.Role, user.IsActive, user.CreatedAt });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "User not found" });

        if (!string.IsNullOrWhiteSpace(req.Username)) user.Username = req.Username;
        if (!string.IsNullOrWhiteSpace(req.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        if (req.Role != null) user.Role = req.Role;
        if (req.IsActive.HasValue) user.IsActive = req.IsActive.Value;

        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.Username, user.Role, user.IsActive, user.CreatedAt });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "User not found" });
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return Ok(new { message = "User deleted" });
    }

    public record CreateUserRequest(string Username, string Password, string? Role);
    public record UpdateUserRequest(string? Username, string? Password, string? Role, bool? IsActive);
}
