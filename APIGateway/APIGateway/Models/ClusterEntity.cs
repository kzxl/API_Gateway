using System.ComponentModel.DataAnnotations;

namespace APIGateway.Models
{
    public class Cluster
    {
        [Key]
        public int Id { get; set; }
        public string ClusterId { get; set; }
        public string DestinationsJson { get; set; } // simple JSON: [{"Id":"dest1","Address":"https://localhost:5001/"}]
    }
}
