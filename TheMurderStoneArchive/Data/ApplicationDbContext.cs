using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Data
{
    // Inheriting from IdentityDbContext gives you user accounts AND custom tables
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
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
        public DbSet<MurderEventVideo> MurderEventVideos { get; set; }
        public DbSet<MurderEventComment> MurderEventComments { get; set; }
        public DbSet<MurderEventEditSuggestion> MurderEventEditSuggestions { get; set; }
        public DbSet<MurderEventEditSuggestionPhoto> MurderEventEditSuggestionPhotos { get; set; }
        public DbSet<MurderEventEditSuggestionVideo> MurderEventEditSuggestionVideos { get; set; }
        public DbSet<MurderEventChangeLogEntry> MurderEventChangeLogEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.PublicUsername)
                .IsUnique();

            builder.Entity<MurderEventEditSuggestion>()
                .HasOne(s => s.MurderEvent)
                .WithMany(m => m.EditSuggestions)
                .HasForeignKey(s => s.MurderEventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MurderEventEditSuggestionPhoto>()
                .HasOne(p => p.MurderEventEditSuggestion)
                .WithMany(s => s.ProposedPhotos)
                .HasForeignKey(p => p.MurderEventEditSuggestionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MurderEventEditSuggestionVideo>()
                .HasOne(v => v.MurderEventEditSuggestion)
                .WithMany(s => s.ProposedVideos)
                .HasForeignKey(v => v.MurderEventEditSuggestionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MurderEventChangeLogEntry>()
                .HasOne(c => c.MurderEvent)
                .WithMany(m => m.ChangeLogEntries)
                .HasForeignKey(c => c.MurderEventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MurderEventComment>()
                .HasOne(c => c.MurderEvent)
                .WithMany(m => m.Comments)
                .HasForeignKey(c => c.MurderEventId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}