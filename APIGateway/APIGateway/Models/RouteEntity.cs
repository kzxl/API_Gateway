using System.ComponentModel.DataAnnotations;

namespace APIGateway.Models;

public class Route
{
    [Key]
    public int Id { get; set; }
    public string RouteId { get; set; } = string.Empty;
    public string MatchPath { get; set; } = string.Empty;
    public string? Methods { get; set; } // comma separated, nullable
    public string ClusterId { get; set; } = string.Empty;
}
