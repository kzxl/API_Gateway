using System;
using System.ComponentModel.DataAnnotations;

namespace APIGateway.NetFramework.Models
{
    public class Cluster
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string ClusterId { get; set; }

        [Required]
        public string DestinationsJson { get; set; } // JSON array of destinations

        public bool EnableHealthCheck { get; set; }

        [MaxLength(500)]
        public string HealthCheckPath { get; set; }

        public int HealthCheckIntervalSeconds { get; set; }
        public int HealthCheckTimeoutSeconds { get; set; }

        [MaxLength(50)]
        public string LoadBalancingPolicy { get; set; } // RoundRobin, LeastRequests

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Cluster()
        {
            ClusterId = string.Empty;
            DestinationsJson = "[]";
            EnableHealthCheck = false;
            HealthCheckPath = "/health";
            HealthCheckIntervalSeconds = 30;
            HealthCheckTimeoutSeconds = 5;
            LoadBalancingPolicy = "RoundRobin";
            CreatedAt = DateTime.UtcNow;
        }
    }
}
