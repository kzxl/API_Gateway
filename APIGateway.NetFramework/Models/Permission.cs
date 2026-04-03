using System;
using System.ComponentModel.DataAnnotations;

namespace APIGateway.NetFramework.Models
{
    /// <summary>
    /// Permission entity for fine-grained access control.
    /// </summary>
    public class Permission
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } // "routes.read", "routes.write"

        [Required]
        [MaxLength(50)]
        public string Resource { get; set; } // "routes", "clusters", "users"

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } // "read", "write", "delete"

        [MaxLength(500)]
        public string Description { get; set; }

        public DateTime CreatedAt { get; set; }

        public Permission()
        {
            Name = string.Empty;
            Resource = string.Empty;
            Action = string.Empty;
            CreatedAt = DateTime.UtcNow;
        }
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
        public string Role { get; set; } // "Admin", "User"

        public int PermissionId { get; set; }
        public virtual Permission Permission { get; set; }

        public RolePermission()
        {
            Role = string.Empty;
        }
    }

    /// <summary>
    /// User-specific permission overrides.
    /// </summary>
    public class UserPermission
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public virtual User User { get; set; }

        public int PermissionId { get; set; }
        public virtual Permission Permission { get; set; }

        public bool IsGranted { get; set; } // true = grant, false = revoke

        public DateTime CreatedAt { get; set; }

        public UserPermission()
        {
            IsGranted = true;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
