using System.ComponentModel.DataAnnotations;

namespace TheMurderStoneArchive.Models
{
    public class MurderEventVideo
    {
        public int Id { get; set; }
        public int MurderEventId { get; set; }
        public MurderEvent MurderEvent { get; set; } = null!;

        [Required]
        [StringLength(2048)]
        public string Url { get; set; } = string.Empty;

        // YouTube video id extracted from Url for embedding
        [StringLength(64)]
        public string? VideoId { get; set; }
    }
}