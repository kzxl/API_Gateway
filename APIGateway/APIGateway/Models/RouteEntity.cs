using System.ComponentModel.DataAnnotations;

namespace APIGateway.Models
{
    public class Route
    {
        [Key]
        public int Id { get; set; }
        public string RouteId { get; set; }
        public string MatchPath { get; set; }
        public string Methods { get; set; } // comma separated
        public string ClusterId { get; set; }
    }
}
