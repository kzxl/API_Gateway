using System;
using System.ComponentModel.DataAnnotations;

namespace APIGateway.NetFramework.Models
{
    public class Route
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string RouteId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ClusterId { get; set; }

        [Required]
        [MaxLength(500)]
        public string MatchPath { get; set; }

        public int RateLimitPerSecond { get; set; }

        public int CircuitBreakerThreshold { get; set; }
        public int CircuitBreakerDurationSeconds { get; set; }

        [MaxLength(1000)]
        public string IpWhitelist { get; set; }

        [MaxLength(1000)]
        public string IpBlacklist { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Route()
        {
            RouteId = string.Empty;
            ClusterId = string.Empty;
            MatchPath = string.Empty;
            RateLimitPerSecond = 0;
            CircuitBreakerThreshold = 0;
            CircuitBreakerDurationSeconds = 30;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
