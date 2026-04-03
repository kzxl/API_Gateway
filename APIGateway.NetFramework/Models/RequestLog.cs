using System;
using System.ComponentModel.DataAnnotations;

namespace APIGateway.NetFramework.Models
{
    public class RequestLog
    {
        [Key]
        public int Id { get; set; }

        public DateTime Timestamp { get; set; }

        [MaxLength(10)]
        public string Method { get; set; }

        [MaxLength(2000)]
        public string Path { get; set; }

        public int StatusCode { get; set; }

        public long LatencyMs { get; set; }

        [MaxLength(45)]
        public string ClientIp { get; set; }

        [MaxLength(100)]
        public string RouteId { get; set; }

        [MaxLength(512)]
        public string UserAgent { get; set; }

        public RequestLog()
        {
            Timestamp = DateTime.UtcNow;
            Method = string.Empty;
            Path = string.Empty;
            ClientIp = string.Empty;
            RouteId = string.Empty;
            UserAgent = string.Empty;
        }
    }
}
