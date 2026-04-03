using System;
using System.ComponentModel.DataAnnotations;

namespace APIGateway.NetFramework.Models
{
    /// <summary>
    /// User session tracking for audit and multi-device management.
    /// </summary>
    public class UserSession
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(64)]
        public string SessionId { get; set; }

        [Required]
        [MaxLength(64)]
        public string AccessTokenJti { get; set; }

        [MaxLength(256)]
        public string RefreshToken { get; set; }

        [MaxLength(45)]
        public string IpAddress { get; set; }

        [MaxLength(512)]
        public string UserAgent { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        // Computed properties
        public bool IsActive
        {
            get { return RevokedAt == null && DateTime.UtcNow < ExpiresAt; }
        }

        // Navigation
        public virtual User User { get; set; }

        public UserSession()
        {
            SessionId = Guid.NewGuid().ToString();
            AccessTokenJti = string.Empty;
            IpAddress = string.Empty;
            UserAgent = string.Empty;
            CreatedAt = DateTime.UtcNow;
            LastActivityAt = DateTime.UtcNow;
        }
    }
}
