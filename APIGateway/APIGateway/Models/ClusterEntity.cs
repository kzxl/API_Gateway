using System.ComponentModel.DataAnnotations;

namespace APIGateway.Models;

public class Cluster
{
    [Key]
    public int Id { get; set; }
    public string ClusterId { get; set; } = string.Empty;
    public string DestinationsJson { get; set; } = "[]"; // [{\"id\":\"dest1\",\"address\":\"https://localhost:5001/\"}]
}
