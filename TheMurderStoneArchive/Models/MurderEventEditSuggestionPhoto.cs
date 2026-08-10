using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace TheMurderStoneArchive.Models
{
    /// <summary>
    /// A photo proposed as part of a pending MurderEventEditSuggestion. Copied into a real
    /// MurderEventPhoto when the suggestion is approved; discarded if rejected.
    /// </summary>
    public class MurderEventEditSuggestionPhoto
    {
        public int Id { get; set; }

        [Required]
        public int MurderEventEditSuggestionId { get; set; }

        [ValidateNever]
        public MurderEventEditSuggestion MurderEventEditSuggestion { get; set; } = null!;

        [Required]
        public string FileName { get; set; } = string.Empty;

        [Required]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public byte[]? Data { get; set; }
    }
}
