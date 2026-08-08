using System.ComponentModel.DataAnnotations;

namespace TheMurderStoneArchive.Models
{
    public class Location
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public ICollection<MurderEvent> MurderEvents { get; set; } = new List<MurderEvent>();
    }
}
