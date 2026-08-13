using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Data
{
    // Inheriting from IdentityDbContext gives you user accounts AND custom tables
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Required by IDataProtectionKeyContext — stores keys in the database
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

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
        public DbSet<CtaClickEvent> CtaClickEvents { get; set; }
        public DbSet<DonationCampaign> DonationCampaigns { get; set; }
        public DbSet<MonetaryContribution> MonetaryContributions { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Enable the PostGIS extension so geometry columns and spatial indexes work.
            builder.HasPostgresExtension("postgis");

            // Spatial index on location coordinates for efficient bounding-box queries.
            builder.Entity<Location>()
                .HasIndex(l => l.Coordinates)
                .HasMethod("GIST");

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.PublicUsername)
                .IsUnique();

            builder.Entity<MurderEventEditSuggestion>()
                .HasOne(s => s.MurderEvent)
                .WithMany(m => m.EditSuggestions)
                .HasForeignKey(s => s.MurderEventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MurderEventEditSuggestion>()
                .HasIndex(s => s.Status);

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

            builder.Entity<CtaClickEvent>()
                .Property(e => e.CtaKey)
                .HasMaxLength(120)
                .IsRequired();

            builder.Entity<CtaClickEvent>()
                .Property(e => e.Path)
                .HasMaxLength(300);

            builder.Entity<CtaClickEvent>()
                .Property(e => e.Referrer)
                .HasMaxLength(600);

            builder.Entity<CtaClickEvent>()
                .Property(e => e.UserId)
                .HasMaxLength(450);

            builder.Entity<CtaClickEvent>()
                .HasIndex(e => e.ClickedAtUtc);

            builder.Entity<CtaClickEvent>()
                .HasIndex(e => e.CtaKey);

            builder.Entity<DonationCampaign>()
                .Property(c => c.Name)
                .HasMaxLength(140)
                .IsRequired();

            builder.Entity<DonationCampaign>()
                .Property(c => c.Slug)
                .HasMaxLength(80)
                .IsRequired();

            builder.Entity<DonationCampaign>()
                .Property(c => c.Description)
                .HasMaxLength(2500);

            builder.Entity<DonationCampaign>()
                .HasIndex(c => c.Slug)
                .IsUnique();

            builder.Entity<MonetaryContribution>()
                .Property(c => c.Currency)
                .HasMaxLength(10)
                .IsRequired();

            builder.Entity<MonetaryContribution>()
                .Property(c => c.Source)
                .HasMaxLength(40)
                .IsRequired();

            builder.Entity<MonetaryContribution>()
                .Property(c => c.Status)
                .HasMaxLength(40)
                .IsRequired();

            builder.Entity<MonetaryContribution>()
                .Property(c => c.ProviderSessionId)
                .HasMaxLength(200);

            builder.Entity<MonetaryContribution>()
                .Property(c => c.ProviderPaymentIntentId)
                .HasMaxLength(200);

            builder.Entity<MonetaryContribution>()
                .Property(c => c.ProviderChargeId)
                .HasMaxLength(200);

            builder.Entity<MonetaryContribution>()
                .Property(c => c.ContributorName)
                .HasMaxLength(160);

            builder.Entity<MonetaryContribution>()
                .Property(c => c.ContributorEmail)
                .HasMaxLength(320);

            builder.Entity<MonetaryContribution>()
                .Property(c => c.Note)
                .HasMaxLength(2000);

            builder.Entity<MonetaryContribution>()
                .HasIndex(c => c.SubmittedAtUtc);

            builder.Entity<MonetaryContribution>()
                .HasIndex(c => c.ProviderPaymentIntentId);

            builder.Entity<MonetaryContribution>()
                .HasOne(c => c.DonationCampaign)
                .WithMany(c => c.Contributions)
                .HasForeignKey(c => c.DonationCampaignId)
                .OnDelete(DeleteBehavior.SetNull);

            // ApiKey configuration
            builder.Entity<ApiKey>()
                .HasIndex(k => k.KeyHash)
                .IsUnique();

            builder.Entity<ApiKey>()
                .HasOne(k => k.User)
                .WithMany()
                .HasForeignKey(k => k.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ApiKey>()
                .HasOne(k => k.Subscription)
                .WithMany()
                .HasForeignKey(k => k.SubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ApiKey>()
                .HasIndex(k => k.IsRevoked);

            builder.Entity<ApiKey>()
                .HasIndex(k => k.CreatedAtUtc);
        }
    }
}
