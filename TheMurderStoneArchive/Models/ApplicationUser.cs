using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TheMurderStoneArchive.Models
{
    /// <summary>
    /// Application user extending IdentityUser with a unique, publicly-facing username.
    /// The Identity <see cref="IdentityUser.UserName"/>/Email remains the login identifier;
    /// <see cref="PublicUsername"/> is what other users see (e.g. on comments, changelog, contributors).
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(30, MinimumLength = 3)]
        public string PublicUsername { get; set; } = string.Empty;
    }
}
