using System;
using System.ComponentModel.DataAnnotations;

namespace APIGateway.NetFramework.Models
{
    /// <summary>
    /// Refresh Token for JWT authentication.
    /// </summary>
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(256)]
        public string Token { get; set; }

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }

        [MaxLength(45)]
        public string CreatedByIp { get; set; }

        public DateTime? RevokedAt { get; set; }

        [MaxLength(45)]
        public string RevokedByIp { get; set; }

        [MaxLength(256)]
        public string ReplacedByToken { get; set; }

        // Computed properties
        public bool IsExpired
        {
            get { return DateTime.UtcNow >= ExpiresAt; }
        }

        public bool IsRevoked
        {
            get { return RevokedAt != null; }
        }

        public bool IsActive
        {
            get { return !IsRevoked && !IsExpired; }
        }

        // Navigation
        public virtual User User { get; set; }

        public RefreshToken()
        {
            Token = string.Empty;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
