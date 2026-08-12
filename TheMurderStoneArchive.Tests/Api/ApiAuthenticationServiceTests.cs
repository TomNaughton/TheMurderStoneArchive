using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Services;
using Xunit;

namespace TheMurderStoneArchive.Tests.Api
{
    public class ApiAuthenticationServiceTests
    {
        private ApplicationDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("ApiAuthTests_" + Guid.NewGuid())
                .Options;
            return new ApplicationDbContext(options);
        }

        private ApiAuthenticationService CreateService(ApplicationDbContext db)
        {
            var logger = new Mock<ILogger<ApiAuthenticationService>>();
            return new ApiAuthenticationService(db, logger.Object);
        }

        // ─── GenerateApiKeyAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GenerateApiKeyAsync_ReturnsRawKeyWithPrefix()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var (rawKey, entity) = await svc.GenerateApiKeyAsync("user1", "Test");

            Assert.StartsWith("msa_", rawKey);
            Assert.NotNull(entity);
            Assert.Equal("user1", entity.UserId);
            Assert.Equal("Test", entity.Name);
            Assert.Equal(ApiKeyTier.Free, entity.Tier);
        }

        [Fact]
        public async Task GenerateApiKeyAsync_StoresHashNotRawKey()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var (rawKey, entity) = await svc.GenerateApiKeyAsync("user1");

            Assert.NotEqual(rawKey, entity.KeyHash);
        }

        [Fact]
        public async Task GenerateApiKeyAsync_EnforcesMaxFreeKeyLimit()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            // Create up to the limit
            for (int i = 0; i < TheMurderStoneArchive.Helpers.AppConstants.MaxFreeApiKeysPerUser; i++)
            {
                await svc.GenerateApiKeyAsync("user1", $"Key{i}");
            }

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.GenerateApiKeyAsync("user1", "OneMore"));
        }

        [Fact]
        public async Task GenerateApiKeyAsync_EnforcesMaxPremiumKeyLimit()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            await svc.GenerateApiKeyAsync("user1", "PKey1", ApiKeyTier.Premium);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.GenerateApiKeyAsync("user1", "PKey2", ApiKeyTier.Premium));
        }

        [Fact]
        public async Task GenerateApiKeyAsync_DifferentUsersCanGenerateIndependently()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var (rawKey1, _) = await svc.GenerateApiKeyAsync("user1");
            var (rawKey2, _) = await svc.GenerateApiKeyAsync("user2");

            Assert.NotEqual(rawKey1, rawKey2);
        }

        // ─── ValidateAndGetApiKeyAsync ────────────────────────────────────────

        [Fact]
        public async Task ValidateAndGetApiKeyAsync_ValidKey_ReturnsEntity()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var (rawKey, created) = await svc.GenerateApiKeyAsync("user1");

            var result = await svc.ValidateAndGetApiKeyAsync(rawKey);

            Assert.NotNull(result);
            Assert.Equal(created.Id, result!.Id);
        }

        [Fact]
        public async Task ValidateAndGetApiKeyAsync_InvalidKey_ReturnsNull()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var result = await svc.ValidateAndGetApiKeyAsync("msa_notarealkey");

            Assert.Null(result);
        }

        [Fact]
        public async Task ValidateAndGetApiKeyAsync_RevokedKey_ReturnsNull()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var (rawKey, entity) = await svc.GenerateApiKeyAsync("user1");
            await svc.RevokeApiKeyAsync(entity.Id);

            var result = await svc.ValidateAndGetApiKeyAsync(rawKey);

            Assert.Null(result);
        }

        [Fact]
        public async Task ValidateAndGetApiKeyAsync_IncrementsRequestCounters()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var (rawKey, entity) = await svc.GenerateApiKeyAsync("user1");

            await svc.ValidateAndGetApiKeyAsync(rawKey);
            var result = await svc.ValidateAndGetApiKeyAsync(rawKey);

            Assert.Equal(2, result!.TotalRequests);
            Assert.Equal(2, result.RequestsThisMonth);
        }

        [Fact]
        public async Task ValidateAndGetApiKeyAsync_ExpiredPremiumKey_DowngradesToFree()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var (rawKey, entity) = await svc.GenerateApiKeyAsync("user1", tier: ApiKeyTier.Premium);
            entity.SubscriptionExpiresAtUtc = DateTime.UtcNow.AddDays(-1); // already expired
            db.ApiKeys.Update(entity);
            await db.SaveChangesAsync();

            var result = await svc.ValidateAndGetApiKeyAsync(rawKey);

            Assert.NotNull(result);
            Assert.Equal(ApiKeyTier.Free, result!.Tier);
            Assert.Null(result.SubscriptionId);
        }

        [Fact]
        public async Task ValidateAndGetApiKeyAsync_NullOrEmpty_ReturnsNull()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            Assert.Null(await svc.ValidateAndGetApiKeyAsync(string.Empty));
            Assert.Null(await svc.ValidateAndGetApiKeyAsync("   "));
        }

        // ─── IsPremiumActive ──────────────────────────────────────────────────

        [Fact]
        public void IsPremiumActive_ActivePremium_ReturnsTrue()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var key = new ApiKey
            {
                Tier = ApiKeyTier.Premium,
                SubscriptionExpiresAtUtc = DateTime.UtcNow.AddDays(30)
            };

            Assert.True(svc.IsPremiumActive(key));
        }

        [Fact]
        public void IsPremiumActive_ExpiredPremium_ReturnsFalse()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var key = new ApiKey
            {
                Tier = ApiKeyTier.Premium,
                SubscriptionExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
            };

            Assert.False(svc.IsPremiumActive(key));
        }

        [Fact]
        public void IsPremiumActive_FreeKey_ReturnsFalse()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var key = new ApiKey { Tier = ApiKeyTier.Free };

            Assert.False(svc.IsPremiumActive(key));
        }

        [Fact]
        public void IsPremiumActive_NullExpiry_ReturnsFalse()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var key = new ApiKey
            {
                Tier = ApiKeyTier.Premium,
                SubscriptionExpiresAtUtc = null
            };

            Assert.False(svc.IsPremiumActive(key));
        }

        // ─── RevokeApiKeyAsync ────────────────────────────────────────────────

        [Fact]
        public async Task RevokeApiKeyAsync_ExistingKey_SetsIsRevoked()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var (_, entity) = await svc.GenerateApiKeyAsync("user1");

            var revoked = await svc.RevokeApiKeyAsync(entity.Id);

            Assert.NotNull(revoked);
            Assert.True(revoked!.IsRevoked);
        }

        [Fact]
        public async Task RevokeApiKeyAsync_NonexistentId_ReturnsNull()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var result = await svc.RevokeApiKeyAsync(99999);

            Assert.Null(result);
        }

        // ─── UpgradeToPremiumpAsync ───────────────────────────────────────────

        [Fact]
        public async Task UpgradeToPremiumAsync_SetsCorrectFields()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var (_, entity) = await svc.GenerateApiKeyAsync("user1");
            var expiry = DateTime.UtcNow.AddMonths(1);

            var result = await svc.UpgradeToPremiumpAsync(entity, 42L, expiry);

            Assert.True(result);
            Assert.Equal(ApiKeyTier.Premium, entity.Tier);
            Assert.Equal(42L, entity.SubscriptionId);
            Assert.Equal(expiry, entity.SubscriptionExpiresAtUtc);
        }

        // ─── GetUserApiKeysAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetUserApiKeysAsync_ReturnsOnlyUserKeys()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            await svc.GenerateApiKeyAsync("user1", "A");
            await svc.GenerateApiKeyAsync("user1", "B");
            await svc.GenerateApiKeyAsync("user2", "C");

            var user1Keys = (await svc.GetUserApiKeysAsync("user1")).ToList();

            Assert.Equal(2, user1Keys.Count);
            Assert.All(user1Keys, k => Assert.Equal("user1", k.UserId));
        }

        // ─── ResetMonthlyLimitsAsync ──────────────────────────────────────────

        [Fact]
        public async Task ResetMonthlyLimitsAsync_ResetsExpiredBillingPeriods()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var (_, entity) = await svc.GenerateApiKeyAsync("user1");
            entity.RequestsThisMonth = 50;
            entity.BillingPeriodStartUtc = DateTime.UtcNow.AddMonths(-2); // old billing period
            db.ApiKeys.Update(entity);
            await db.SaveChangesAsync();

            await svc.ResetMonthlyLimitsAsync();

            var refreshed = await db.ApiKeys.FindAsync(entity.Id);
            Assert.Equal(0, refreshed!.RequestsThisMonth);
        }

        [Fact]
        public async Task ResetMonthlyLimitsAsync_DoesNotResetActiveBillingPeriod()
        {
            using var db = CreateDb();
            var svc = CreateService(db);

            var (_, entity) = await svc.GenerateApiKeyAsync("user1");
            entity.RequestsThisMonth = 50;
            entity.BillingPeriodStartUtc = DateTime.UtcNow; // current period
            db.ApiKeys.Update(entity);
            await db.SaveChangesAsync();

            await svc.ResetMonthlyLimitsAsync();

            var refreshed = await db.ApiKeys.FindAsync(entity.Id);
            Assert.Equal(50, refreshed!.RequestsThisMonth);
        }
    }
}
