using System.ComponentModel.DataAnnotations;

namespace TheMurderStoneArchive.Models
{
    public class Perpetrator
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(255)]
        public string Punishment { get; set; } = string.Empty;

        public int MurderEventId { get; set; }
        public MurderEvent MurderEvent { get; set; } = null!;
    }
}
