using System.ComponentModel.DataAnnotations;

namespace TheMurderStoneArchive.Models
{
    public class MurderEventPhoto
    {
        public int Id { get; set; }

        [Required]
        public int MurderEventId { get; set; }
        public MurderEvent MurderEvent { get; set; } = null!;

        [Required]
        public string FilePath { get; set; } = string.Empty; // relative path under wwwroot

        [Required]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }
        // Optional: binary data stored in the database when filesystem storage is not used
        public byte[]? Data { get; set; }
    }
}
