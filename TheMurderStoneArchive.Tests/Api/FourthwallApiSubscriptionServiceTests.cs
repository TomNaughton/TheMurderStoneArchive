using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Services;
using Xunit;

namespace TheMurderStoneArchive.Tests.Api
{
    public class FourthwallApiSubscriptionServiceTests
    {
        private static ApplicationDbContext CreateDb()
        {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("FwSubTests_" + Guid.NewGuid())
                .Options;
            return new ApplicationDbContext(opts);
        }

        private static (FourthwallApiSubscriptionService svc, Mock<IApiAuthenticationService> authMock)
            CreateService(ApplicationDbContext db)
        {
            var authMock = new Mock<IApiAuthenticationService>();
            var logger = new Mock<ILogger<FourthwallApiSubscriptionService>>();
            var svc = new FourthwallApiSubscriptionService(db, authMock.Object, logger.Object);
            return (svc, authMock);
        }

        private static ApplicationUser CreateUser(ApplicationDbContext db,
            string email = "test@test.com", string id = "user_1")
        {
            var user = new ApplicationUser
            {
                Id = id,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString()
            };
            db.Users.Add(user);
            db.SaveChanges();
            return user;
        }

        // ─── ProcessSubscriptionPurchaseAsync ─────────────────────────────────

        [Fact]
        public async Task ProcessSubscriptionPurchaseAsync_UserNotFound_ReturnsFalse()
        {
            using var db = CreateDb();
            var (svc, _) = CreateService(db);

            var result = await svc.ProcessSubscriptionPurchaseAsync(
                "unknown@unknown.com", "order_1", DateTime.UtcNow.AddMonths(1));

            Assert.False(result);
        }

        [Fact]
        public async Task ProcessSubscriptionPurchaseAsync_EmptyEmail_ReturnsFalse()
        {
            using var db = CreateDb();
            var (svc, _) = CreateService(db);

            var result = await svc.ProcessSubscriptionPurchaseAsync(
                "", "order_1", DateTime.UtcNow.AddMonths(1));

            Assert.False(result);
        }

        [Fact]
        public async Task ProcessSubscriptionPurchaseAsync_EmptyOrderId_ReturnsFalse()
        {
            using var db = CreateDb();
            var (svc, _) = CreateService(db);

            var result = await svc.ProcessSubscriptionPurchaseAsync(
                "test@test.com", "", DateTime.UtcNow.AddMonths(1));

            Assert.False(result);
        }

        [Fact]
        public async Task ProcessSubscriptionPurchaseAsync_ValidUser_CreatesSubscription()
        {
            using var db = CreateDb();
            var user = CreateUser(db, "buyer@test.com", "user_1");

            var authMock = new Mock<IApiAuthenticationService>();
            var fakeApiKey = new ApiKey { Id = 1, UserId = user.Id, Tier = ApiKeyTier.Premium,
                KeyHash = "hash1", BillingPeriodStartUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow };
            db.ApiKeys.Add(fakeApiKey);
            await db.SaveChangesAsync();
            authMock.Setup(s => s.GenerateApiKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ApiKeyTier>()))
                .ReturnsAsync(("msa_generated", fakeApiKey));

            var logger = new Mock<ILogger<FourthwallApiSubscriptionService>>();
            var svc = new FourthwallApiSubscriptionService(db, authMock.Object, logger.Object);

            var expiry = DateTime.UtcNow.AddMonths(1);
            var result = await svc.ProcessSubscriptionPurchaseAsync("buyer@test.com", "order_99", expiry);

            Assert.True(result);
            var subscription = await db.Subscriptions.FirstOrDefaultAsync(s => s.ExternalId == "order_99");
            Assert.NotNull(subscription);
            Assert.Equal(4.00m, subscription!.MonthlyAmountGbp);
            Assert.True(subscription.ExpiresAtUtc >= expiry.AddSeconds(-5));
        }

        [Fact]
        public async Task ProcessSubscriptionPurchaseAsync_ExistingSubscription_UpdatesExpiry()
        {
            using var db = CreateDb();
            var user = CreateUser(db, "buyer@test.com", "user_1");

            db.Subscriptions.Add(new Subscription
            {
                Id = 1,
                ExternalId = "order_200",
                ContributorEmail = "buyer@test.com",
                MonthlyAmountGbp = 4.00m,
                StartedAtUtc = DateTime.UtcNow.AddMonths(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(3),
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-1),
                UpdatedAtUtc = DateTime.UtcNow
            });
            var fakeApiKey = new ApiKey { Id = 2, UserId = user.Id, Tier = ApiKeyTier.Premium,
                KeyHash = "hash2", BillingPeriodStartUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow };
            db.ApiKeys.Add(fakeApiKey);
            await db.SaveChangesAsync();

            var authMock = new Mock<IApiAuthenticationService>();
            authMock.Setup(s => s.GenerateApiKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ApiKeyTier>()))
                .ReturnsAsync(("msa_generated", fakeApiKey));
            var logger = new Mock<ILogger<FourthwallApiSubscriptionService>>();
            var svc = new FourthwallApiSubscriptionService(db, authMock.Object, logger.Object);

            var newExpiry = DateTime.UtcNow.AddMonths(1);
            var result = await svc.ProcessSubscriptionPurchaseAsync(
                "buyer@test.com", "order_200", newExpiry);

            Assert.True(result);
            var subscription = await db.Subscriptions.FindAsync(1L);
            Assert.True(subscription!.ExpiresAtUtc >= newExpiry.AddSeconds(-5));
            Assert.Null(subscription.CancelledAtUtc);
        }

        // ─── ProcessSubscriptionCancellationAsync ─────────────────────────────

        [Fact]
        public async Task ProcessSubscriptionCancellationAsync_UserNotFound_ReturnsFalse()
        {
            using var db = CreateDb();
            var (svc, _) = CreateService(db);

            var result = await svc.ProcessSubscriptionCancellationAsync(
                "nobody@test.com", "order_1");

            Assert.False(result);
        }

        [Fact]
        public async Task ProcessSubscriptionCancellationAsync_EmptyArgs_ReturnsFalse()
        {
            using var db = CreateDb();
            var (svc, _) = CreateService(db);

            Assert.False(await svc.ProcessSubscriptionCancellationAsync("", "order_1"));
            Assert.False(await svc.ProcessSubscriptionCancellationAsync("test@test.com", ""));
        }

        [Fact]
        public async Task ProcessSubscriptionCancellationAsync_ValidUser_NoKey_ReturnsTrue()
        {
            using var db = CreateDb();
            CreateUser(db, "cancel@test.com", "user_cancel");
            var (svc, _) = CreateService(db);

            // User exists but has no premium key – service should still return true (graceful)
            var result = await svc.ProcessSubscriptionCancellationAsync(
                "cancel@test.com", "nonexistent_order");

            Assert.True(result);
        }

        // ─── GetOrCreateSubscriptionApiKeyAsync ───────────────────────────────

        [Fact]
        public async Task GetOrCreateSubscriptionApiKeyAsync_ExistingPremiumKey_UpdatesExpiry()
        {
            using var db = CreateDb();
            var user = CreateUser(db);
            var subscription = new Subscription
            {
                Id = 5,
                ExternalId = "sub_abc",
                ContributorEmail = user.Email!,
                MonthlyAmountGbp = 4.00m,
                StartedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var existingKey = new ApiKey
            {
                Id = 20,
                UserId = user.Id,
                Tier = ApiKeyTier.Premium,
                KeyHash = "hashpremium",
                SubscriptionId = 5L,
                SubscriptionExpiresAtUtc = DateTime.UtcNow.AddDays(5),
                BillingPeriodStartUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                Subscription = subscription
            };
            db.ApiKeys.Add(existingKey);
            await db.SaveChangesAsync();

            var (svc, _) = CreateService(db);
            var newExpiry = DateTime.UtcNow.AddMonths(1);

            var result = await svc.GetOrCreateSubscriptionApiKeyAsync(
                user.Id, "sub_abc", newExpiry);

            Assert.NotNull(result);
            Assert.Equal(20, result!.Id);
            // Expiry should have been updated
            var refreshed = await db.ApiKeys.FindAsync(20);
            Assert.True(refreshed!.SubscriptionExpiresAtUtc >= newExpiry.AddSeconds(-5));
        }

        [Fact]
        public async Task GetOrCreateSubscriptionApiKeyAsync_FreeKeyExists_CreatesNewPremiumKey()
        {
            using var db = CreateDb();
            var user = CreateUser(db);

            var freeKey = new ApiKey
            {
                Id = 30,
                UserId = user.Id,
                Tier = ApiKeyTier.Free,
                KeyHash = "hashfree",
                BillingPeriodStartUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.ApiKeys.Add(freeKey);

            var subscription = new Subscription
            {
                Id = 7,
                ExternalId = "sub_new",
                ContributorEmail = user.Email!,
                MonthlyAmountGbp = 4.00m,
                StartedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync();

            var (svc, authMock) = CreateService(db);
            var newPremiumKey = new ApiKey { Id = 99, UserId = user.Id, Tier = ApiKeyTier.Premium,
                KeyHash = "hashprem", BillingPeriodStartUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow };
            db.ApiKeys.Add(newPremiumKey);
            await db.SaveChangesAsync();
            authMock.Setup(s => s.GenerateApiKeyAsync(user.Id, It.IsAny<string>(), ApiKeyTier.Premium))
                .ReturnsAsync(("msa_new_key", newPremiumKey));

            var expiry = DateTime.UtcNow.AddMonths(1);
            var result = await svc.GetOrCreateSubscriptionApiKeyAsync(user.Id, "sub_new", expiry);

            // A brand-new Premium key is returned
            Assert.NotNull(result);
            Assert.Equal(ApiKeyTier.Premium, result!.Tier);

            // The original free key is unchanged
            var unchanged = await db.ApiKeys.FindAsync(30);
            Assert.Equal(ApiKeyTier.Free, unchanged!.Tier);
        }
    }
}
