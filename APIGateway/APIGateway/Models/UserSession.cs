using System.ComponentModel.DataAnnotations;

namespace APIGateway.Models;

/// <summary>
/// User session tracking for audit and multi-device management.
/// UArch: Enables session revocation and security monitoring.
/// </summary>
public class UserSession
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [Required]
    [MaxLength(64)]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(64)]
    public string AccessTokenJti { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? RefreshToken { get; set; }

    [MaxLength(45)]
    public string IpAddress { get; set; } = string.Empty;

    [MaxLength(512)]
    public string UserAgent { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Computed properties
    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;

    // Navigation
    public User User { get; set; } = null!;
}
