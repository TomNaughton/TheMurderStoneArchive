using System.ComponentModel.DataAnnotations;

namespace TheMurderStoneArchive.Models
{
    /// <summary>
    /// A user-submitted comment on a MurderEvent. Visible to everyone once posted.
    /// </summary>
    public class MurderEventComment
    {
        public int Id { get; set; }

        public int MurderEventId { get; set; }
        public MurderEvent MurderEvent { get; set; } = null!;

        [Required]
        [StringLength(2000)]
        public string Content { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
