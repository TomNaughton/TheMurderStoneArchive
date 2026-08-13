using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TheMurderStoneArchive.Models
{
    public enum EditSuggestionStatus
    {
        Pending,
        Approved,
        Rejected
    }

    /// <summary>
    /// A proposed edit to a MurderEvent submitted by a user who is neither the creator
    /// nor an admin. Must be approved by an admin before the changes become visible.
    /// </summary>
    public class MurderEventEditSuggestion
    {
        public int Id { get; set; }

        public int MurderEventId { get; set; }

        [ValidateNever]
        public MurderEvent MurderEvent { get; set; } = null!;

        [ValidateNever]
        public string SubmittedById { get; set; } = string.Empty;

        [ValidateNever]
        public ApplicationUser? SubmittedBy { get; set; }

        public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;

        public EditSuggestionStatus Status { get; set; } = EditSuggestionStatus.Pending;

        // Proposed field values
        [Required]
        [StringLength(150)]
        public string ProposedTitle { get; set; } = string.Empty;

        public int ProposedYear { get; set; }

        [Required]
        public string ProposedDescription { get; set; } = string.Empty;

        [Required]
        public StoneCategory ProposedCategory { get; set; }

        public bool ProposedIsProtected { get; set; }

        public bool ProposedIsLost { get; set; }

        // Proposed location fields (mirrors MurderEvent.Location scalars)
        [StringLength(100)]
        public string? ProposedLocationName { get; set; }

        public double ProposedLatitude { get; set; }
        public double ProposedLongitude { get; set; }

        /// <summary>
        /// Comma-separated list of existing MurderEventPhoto ids the submitter proposed removing.
        /// </summary>
        public string? ProposedDeletedPhotoIds { get; set; }

        /// <summary>
        /// New photos proposed to be added, pending admin approval.
        /// </summary>
        [ValidateNever]
        public ICollection<MurderEventEditSuggestionPhoto> ProposedPhotos { get; set; } = new List<MurderEventEditSuggestionPhoto>();

        /// <summary>
        /// Proposed replacement set of YouTube video links.
        /// </summary>
        [ValidateNever]
        public ICollection<MurderEventEditSuggestionVideo> ProposedVideos { get; set; } = new List<MurderEventEditSuggestionVideo>();

        /// <summary>
        /// Optional note from the submitter explaining the reasoning for the edit.
        /// </summary>
        [StringLength(1000)]
        public string? SubmissionNote { get; set; }

        // Review metadata
        public string? ReviewedById { get; set; }

        [ValidateNever]
        public ApplicationUser? ReviewedBy { get; set; }
        public DateTime? ReviewedUtc { get; set; }

        [StringLength(1000)]
        public string? ReviewNotes { get; set; }
    }
}
