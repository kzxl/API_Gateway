using System.ComponentModel.DataAnnotations;

namespace APIGateway.Models;

/// <summary>
/// Refresh Token for JWT authentication.
/// UArch: Immutable record for thread-safety and performance.
/// </summary>
public class RefreshToken
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(45)]
    public string? CreatedByIp { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(45)]
    public string? RevokedByIp { get; set; }

    [MaxLength(256)]
    public string? ReplacedByToken { get; set; }

    // Computed properties (not stored in DB)
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation
    public User User { get; set; } = null!;
}
