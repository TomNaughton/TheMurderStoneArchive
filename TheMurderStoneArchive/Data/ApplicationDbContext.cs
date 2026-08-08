using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Data
{
    // Inheriting from IdentityDbContext gives you user accounts AND custom tables
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<MurderEvent> MurderEvents { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Perpetrator> Perpetrators { get; set; }
        public DbSet<Monument> Monuments { get; set; }
        public DbSet<MurderEventPhoto> MurderEventPhotos { get; set; }
    }
}