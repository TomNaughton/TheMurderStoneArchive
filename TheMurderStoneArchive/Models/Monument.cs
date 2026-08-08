using System.ComponentModel.DataAnnotations;

namespace TheMurderStoneArchive.Models
{
    public class Monument
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string MonumentType { get; set; } = string.Empty;

        [Required]
        public string Inscription { get; set; } = string.Empty;

        [StringLength(255)]
        public string FundedBy { get; set; } = string.Empty;

        public int MurderEventId { get; set; }
        public MurderEvent MurderEvent { get; set; } = null!;
    }
}
