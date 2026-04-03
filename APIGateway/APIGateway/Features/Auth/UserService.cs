using APIGateway.Core.Constants;
using APIGateway.Core.Interfaces;
using APIGateway.Core.Interfaces.DTOs;
using APIGateway.Data;
using Microsoft.EntityFrameworkCore;

namespace APIGateway.Features.Auth;

public class UserService : IUserService
{
    private readonly GatewayDbContext _db;

    public UserService(GatewayDbContext db) => _db = db;

    public async Task<List<UserDto>> GetAllAsync()
    {
        return await _db.Users
            .Select(u => new UserDto(u.Id, u.Username, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync();
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
            throw new InvalidOperationException("Username already exists");

        var user = new Models.User
        {
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role ?? Roles.User,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return new UserDto(user.Id, user.Username, user.Role, user.IsActive, user.CreatedAt);
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Username)) user.Username = dto.Username;
        if (!string.IsNullOrWhiteSpace(dto.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        if (dto.Role != null) user.Role = dto.Role;
        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;

        await _db.SaveChangesAsync();
        return new UserDto(user.Id, user.Username, user.Role, user.IsActive, user.CreatedAt);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return false;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<UserDto?> ValidateCredentialsAsync(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;
        return new UserDto(user.Id, user.Username, user.Role, user.IsActive, user.CreatedAt);
    }

    public async Task<UserDto?> GetByIdAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return null;
        return new UserDto(user.Id, user.Username, user.Role, user.IsActive, user.CreatedAt);
    }

    public async Task<UserDto?> GetByUsernameAsync(string username)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return null;
        return new UserDto(user.Id, user.Username, user.Role, user.IsActive, user.CreatedAt)
        {
            FailedLoginAttempts = user.FailedLoginAttempts,
            LockedUntil = user.LockedUntil,
            IsLocked = user.IsLocked
        };
    }

    public async Task IncrementFailedLoginAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        user.FailedLoginAttempts++;
        user.LastFailedLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task ResetFailedLoginAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        user.FailedLoginAttempts = 0;
        user.LastFailedLogin = null;
        user.LockedUntil = null;
        await _db.SaveChangesAsync();
    }

    public async Task LockAccountAsync(int userId, TimeSpan duration)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        user.LockedUntil = DateTime.UtcNow.Add(duration);
        await _db.SaveChangesAsync();
    }
}
