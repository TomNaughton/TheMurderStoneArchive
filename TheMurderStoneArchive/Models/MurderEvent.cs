using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;

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
    }
}
