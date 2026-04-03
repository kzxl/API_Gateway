using System.ComponentModel.DataAnnotations;

namespace APIGateway.Models;

/// <summary>
/// Permission entity for fine-grained access control.
/// UArch: Immutable, cacheable for performance.
/// </summary>
public class Permission
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // "routes.read", "routes.write"

    [Required]
    [MaxLength(50)]
    public string Resource { get; set; } = string.Empty; // "routes", "clusters", "users"

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty; // "read", "write", "delete"

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Role-Permission mapping.
/// </summary>
public class RolePermission
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = string.Empty; // "Admin", "User"

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

/// <summary>
/// User-specific permission overrides.
/// </summary>
public class UserPermission
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    public bool IsGranted { get; set; } = true; // true = grant, false = revoke

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
