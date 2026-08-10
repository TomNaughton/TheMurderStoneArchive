using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TheMurderStoneArchive.Models
{
    public enum StoneCategory
    {
        [Display(Name = "Confirmed Murder Stone")]
        Confirmed,
        [Display(Name = "Folkloric / Legend")]
        Folkloric,
        [Display(Name = "Pudding Stone")]
        PuddingStone,
        [Display(Name = "Boundary Marker")]
        BoundaryMarker
    }


    public class MurderEvent
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        public int Year { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public StoneCategory Category { get; set; } = StoneCategory.Confirmed;

        public bool IsApproved { get; set; } = true; // Admin entries are auto-approved; public ones need review

        public bool IsProtected { get; set; } = false; // Historic England listed landmark status

        public bool IsLost { get; set; } = false; // Track destroyed or missing stones

        // Navigation Properties
        public int LocationId { get; set; }
        public Location Location { get; set; } = null!;

        public ICollection<Perpetrator> Perpetrators { get; set; } = new List<Perpetrator>();
        public ICollection<Monument> Monuments { get; set; } = new List<Monument>();
        public ICollection<MurderEventPhoto> Photos { get; set; } = new List<MurderEventPhoto>();
        public ICollection<MurderEventVideo> Videos { get; set; } = new List<MurderEventVideo>();
        // Consent/acknowledgement stored when a user submits or creates an event
        public bool ConfirmRightsAndTerms { get; set; } = false;
        public DateTime? ConsentDateUtc { get; set; }

        // Link to the user who created/submitted this event
        // Stored as the Identity user Id (string)
        public string? CreatedById { get; set; }
        public ApplicationUser? CreatedBy { get; set; }

        public ICollection<MurderEventComment> Comments { get; set; } = new List<MurderEventComment>();
        public ICollection<MurderEventEditSuggestion> EditSuggestions { get; set; } = new List<MurderEventEditSuggestion>();
        public ICollection<MurderEventChangeLogEntry> ChangeLogEntries { get; set; } = new List<MurderEventChangeLogEntry>();

        // Audit Trail Properties
        /// <summary>
        /// UTC timestamp when the record was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the record was last modified.
        /// </summary>
        public DateTime? ModifiedUtc { get; set; }

        /// <summary>
        /// User ID of the person who last modified this record.
        /// </summary>
        public string? ModifiedById { get; set; }
        public ApplicationUser? ModifiedBy { get; set; }

        /// <summary>
        /// UTC timestamp when the record was deleted (soft delete).
        /// </summary>
        public DateTime? DeletedUtc { get; set; }

        /// <summary>
        /// User ID of the person who deleted this record.
        /// </summary>
        public string? DeletedById { get; set; }
        public ApplicationUser? DeletedBy { get; set; }

        /// <summary>
        /// Indicates whether this record has been soft-deleted.
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// Reason for the last modification (optional, for audit logging).
        /// </summary>
        public string? ModificationReason { get; set; }
    }
}
