using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using APIGateway.NetFramework.Data;
using APIGateway.NetFramework.DTOs;
using APIGateway.NetFramework.Models;

namespace APIGateway.NetFramework.Services
{
    public class UserService : IUserService
    {
        private readonly GatewayDbContext _db;

        public UserService(GatewayDbContext db)
        {
            _db = db;
        }

        public async Task<UserDto> GetByIdAsync(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return null;

            return MapToDto(user);
        }

        public async Task<UserDto> GetByUsernameAsync(string username)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return null;

            return MapToDto(user);
        }

        public async Task<UserDto> ValidateCredentialsAsync(string username, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
            if (user == null) return null;

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                return null;
            }

            return MapToDto(user);
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

        private UserDto MapToDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                FailedLoginAttempts = user.FailedLoginAttempts,
                LockedUntil = user.LockedUntil,
                IsLocked = user.IsLocked
            };
        }
    }
}
