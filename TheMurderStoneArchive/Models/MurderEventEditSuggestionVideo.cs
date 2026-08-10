using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TheMurderStoneArchive.Models
{
    /// <summary>
    /// A YouTube video link proposed as part of a pending MurderEventEditSuggestion.
    /// Replaces the live MurderEvent's videos when the suggestion is approved.
    /// </summary>
    public class MurderEventEditSuggestionVideo
    {
        public int Id { get; set; }

        [Required]
        public int MurderEventEditSuggestionId { get; set; }

        [ValidateNever]
        public MurderEventEditSuggestion MurderEventEditSuggestion { get; set; } = null!;

        [Required]
        [StringLength(2048)]
        public string Url { get; set; } = string.Empty;

        [StringLength(64)]
        public string? VideoId { get; set; }
    }
}
