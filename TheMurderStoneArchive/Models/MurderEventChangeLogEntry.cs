namespace TheMurderStoneArchive.Models
{
    /// <summary>
    /// A publicly-viewable record of an approved edit to a MurderEvent, including
    /// who contributed the change and a human-readable summary of what changed.
    /// </summary>
    public class MurderEventChangeLogEntry
    {
        public int Id { get; set; }

        public int MurderEventId { get; set; }
        public MurderEvent MurderEvent { get; set; } = null!;

        /// <summary>
        /// The user who originally suggested/contributed the change.
        /// </summary>
        public string ContributorId { get; set; } = string.Empty;
        public ApplicationUser? Contributor { get; set; }

        /// <summary>
        /// The admin who approved and applied the change.
        /// </summary>
        public string ApprovedById { get; set; } = string.Empty;
        public ApplicationUser? ApprovedBy { get; set; }

        public DateTime ChangeUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Human-readable summary of what fields changed (e.g. "Title: 'Old' -> 'New'; Year: 1850 -> 1852").
        /// </summary>
        public string Summary { get; set; } = string.Empty;
    }
}
