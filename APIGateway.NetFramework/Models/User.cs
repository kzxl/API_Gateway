using System;
using System.ComponentModel.DataAnnotations;

namespace APIGateway.NetFramework.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; } // Admin, User
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        // Account lockout fields
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockedUntil { get; set; }
        public DateTime? LastFailedLogin { get; set; }

        // Computed property
        public bool IsLocked
        {
            get { return LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow; }
        }

        public User()
        {
            Username = string.Empty;
            PasswordHash = string.Empty;
            Role = "User";
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
            FailedLoginAttempts = 0;
        }
    }
}
