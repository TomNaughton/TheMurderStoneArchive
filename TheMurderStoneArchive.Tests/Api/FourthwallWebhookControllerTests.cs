using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TheMurderStoneArchive.Controllers.Api.v1;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Services;
using Xunit;

namespace TheMurderStoneArchive.Tests.Api
{
    public class FourthwallWebhookControllerTests
    {
        private const string TestSecret = "test_webhook_secret_abc123";

        /// <summary>
        /// Builds a controller with a signed fake HttpContext body so that
        /// ReadAndVerifyAsync() succeeds. Pass <c>secret: null</c> to omit the
        /// signature header (simulates a missing / wrong secret).
        /// </summary>
        private static FourthwallWebhookController CreateController(
            IFourthwallApiSubscriptionService? service = null,
            FourthwallWebhookPayload? payload = null,
            string? secret = TestSecret)
        {
            var svc    = service ?? new Mock<IFourthwallApiSubscriptionService>().Object;
            var opts   = Options.Create(new DonationOptions
            {
                FourthwallApiSubscriptionWebhookSecret = secret ?? string.Empty
            });
            var logger     = new Mock<ILogger<FourthwallWebhookController>>().Object;
            var controller = new FourthwallWebhookController(svc, opts, logger);

            var json      = payload is null ? "{}" : JsonSerializer.Serialize(payload);
            var bodyBytes = Encoding.UTF8.GetBytes(json);

            var sigHeader = string.Empty;
            if (secret is not null)
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                sigHeader = Convert.ToHexString(hmac.ComputeHash(bodyBytes)).ToLowerInvariant();
            }

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Body        = new MemoryStream(bodyBytes);
            httpContext.Request.ContentType = "application/json";
            httpContext.Request.Headers["X-Fourthwall-Hmac-Sha256"] = sigHeader;

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static FourthwallWebhookPayload ValidPurchasePayload(
            string email = "buyer@test.com", string orderId = "order_123") =>
            new()
            {
                Type = "SUBSCRIPTION_PURCHASED",
                CreatedAt = DateTime.UtcNow,
                Data = new FourthwallWebhookData
                {
                    Id = orderId, Email = email, CreatedAt = DateTime.UtcNow,
                    Subscription = new FourthwallSubscriptionData
                    {
                        Type = "MONTHLY",
                        Variant = new FourthwallSubscriptionVariant { Id = "variant_1" }
                    }
                }
            };

        private static FourthwallWebhookPayload ValidExpiryPayload(
            string email = "buyer@test.com", string orderId = "order_123") =>
            new()
            {
                Type = "SUBSCRIPTION_EXPIRED",
                CreatedAt = DateTime.UtcNow,
                Data = new FourthwallWebhookData
                {
                    Id = orderId, Email = email, CreatedAt = DateTime.UtcNow,
                    Subscription = new FourthwallSubscriptionData { Type = "MONTHLY" }
                }
            };

        private static FourthwallWebhookPayload ValidChangedPayload(
            string email = "buyer@test.com", string orderId = "order_123") =>
            new()
            {
                Type = "SUBSCRIPTION_CHANGED",
                CreatedAt = DateTime.UtcNow,
                Data = new FourthwallWebhookData
                {
                    Id = orderId, Email = email, CreatedAt = DateTime.UtcNow,
                    Subscription = new FourthwallSubscriptionData { Type = "ANNUAL" }
                }
            };

        // ─── Signature verification ────────────────────────────────────────────

        [Fact]
        public async Task SubscriptionActivated_NoSecretConfigured_Returns401()
        {
            var controller = CreateController(payload: ValidPurchasePayload(), secret: null);
            Assert.IsType<UnauthorizedObjectResult>(await controller.SubscriptionActivated());
        }

        [Fact]
        public async Task SubscriptionActivated_WrongSignature_Returns401()
        {
            var payload   = ValidPurchasePayload();
            var json      = JsonSerializer.Serialize(payload);
            var bodyBytes = Encoding.UTF8.GetBytes(json);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("wrong_secret"));
            var badSig = Convert.ToHexString(hmac.ComputeHash(bodyBytes)).ToLowerInvariant();

            var opts   = Options.Create(new DonationOptions { FourthwallApiSubscriptionWebhookSecret = TestSecret });
            var logger = new Mock<ILogger<FourthwallWebhookController>>().Object;
            var controller = new FourthwallWebhookController(
                new Mock<IFourthwallApiSubscriptionService>().Object, opts, logger);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Body        = new MemoryStream(bodyBytes);
            httpContext.Request.ContentType = "application/json";
            httpContext.Request.Headers["X-Fourthwall-Hmac-Sha256"] = badSig;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            Assert.IsType<UnauthorizedObjectResult>(await controller.SubscriptionActivated());
        }

        // ─── SubscriptionActivated ─────────────────────────────────────────────

        [Fact]
        public async Task SubscriptionActivated_ValidPayload_Returns200()
        {
            var svcMock = new Mock<IFourthwallApiSubscriptionService>();
            svcMock.Setup(s => s.ProcessSubscriptionPurchaseAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(true);

            var result = await CreateController(svcMock.Object, ValidPurchasePayload()).SubscriptionActivated();
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task SubscriptionActivated_WrongEventType_Returns400()
        {
            var payload = ValidPurchasePayload();
            payload.Type = "SUBSCRIPTION_EXPIRED";
            Assert.IsType<BadRequestObjectResult>(
                await CreateController(payload: payload).SubscriptionActivated());
        }

        [Fact]
        public async Task SubscriptionActivated_MissingEmail_Returns400()
        {
            var payload = ValidPurchasePayload();
            payload.Data!.Email = null;
            Assert.IsType<BadRequestObjectResult>(
                await CreateController(payload: payload).SubscriptionActivated());
        }

        [Fact]
        public async Task SubscriptionActivated_MissingOrderId_Returns400()
        {
            var payload = ValidPurchasePayload();
            payload.Data!.Id = null;
            payload.Data.Subscription!.Variant = null;
            Assert.IsType<BadRequestObjectResult>(
                await CreateController(payload: payload).SubscriptionActivated());
        }

        [Fact]
        public async Task SubscriptionActivated_ServiceFails_Returns400()
        {
            var svcMock = new Mock<IFourthwallApiSubscriptionService>();
            svcMock.Setup(s => s.ProcessSubscriptionPurchaseAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(false);

            Assert.IsType<BadRequestObjectResult>(
                await CreateController(svcMock.Object, ValidPurchasePayload()).SubscriptionActivated());
        }

        [Fact]
        public async Task SubscriptionActivated_AnnualSubscription_CallsServiceWithOneYearExpiry()
        {
            DateTime capturedExpiry = default;
            var svcMock = new Mock<IFourthwallApiSubscriptionService>();
            svcMock.Setup(s => s.ProcessSubscriptionPurchaseAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .Callback<string, string, DateTime>((_, _, exp) => capturedExpiry = exp)
                .ReturnsAsync(true);

            var payload = ValidPurchasePayload();
            payload.Data!.Subscription!.Type = "ANNUAL";
            payload.Data.CreatedAt = DateTime.UtcNow;

            await CreateController(svcMock.Object, payload).SubscriptionActivated();

            Assert.True(capturedExpiry > DateTime.UtcNow.AddMonths(11));
            Assert.True(capturedExpiry < DateTime.UtcNow.AddMonths(13));
        }

        [Fact]
        public async Task SubscriptionActivated_MonthlySubscription_CallsServiceWithOneMonthExpiry()
        {
            DateTime capturedExpiry = default;
            var svcMock = new Mock<IFourthwallApiSubscriptionService>();
            svcMock.Setup(s => s.ProcessSubscriptionPurchaseAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .Callback<string, string, DateTime>((_, _, exp) => capturedExpiry = exp)
                .ReturnsAsync(true);

            await CreateController(svcMock.Object, ValidPurchasePayload()).SubscriptionActivated();

            Assert.True(capturedExpiry > DateTime.UtcNow.AddDays(25));
            Assert.True(capturedExpiry < DateTime.UtcNow.AddDays(35));
        }

        // ─── SubscriptionExpired ───────────────────────────────────────────────

        [Fact]
        public async Task SubscriptionExpired_ValidPayload_Returns200()
        {
            var svcMock = new Mock<IFourthwallApiSubscriptionService>();
            svcMock.Setup(s => s.ProcessSubscriptionCancellationAsync(
                    It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            Assert.IsType<OkObjectResult>(
                await CreateController(svcMock.Object, ValidExpiryPayload()).SubscriptionExpired());
        }

        [Fact]
        public async Task SubscriptionExpired_WrongEventType_Returns400()
        {
            var payload = ValidExpiryPayload();
            payload.Type = "SUBSCRIPTION_PURCHASED";
            Assert.IsType<BadRequestObjectResult>(
                await CreateController(payload: payload).SubscriptionExpired());
        }

        [Fact]
        public async Task SubscriptionExpired_MissingEmail_Returns400()
        {
            var payload = ValidExpiryPayload();
            payload.Data!.Email = null;
            Assert.IsType<BadRequestObjectResult>(
                await CreateController(payload: payload).SubscriptionExpired());
        }

        [Fact]
        public async Task SubscriptionExpired_MissingOrderId_Returns400()
        {
            var payload = ValidExpiryPayload();
            payload.Data!.Id = null;
            Assert.IsType<BadRequestObjectResult>(
                await CreateController(payload: payload).SubscriptionExpired());
        }

        [Fact]
        public async Task SubscriptionExpired_ServiceFails_Returns400()
        {
            var svcMock = new Mock<IFourthwallApiSubscriptionService>();
            svcMock.Setup(s => s.ProcessSubscriptionCancellationAsync(
                    It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            Assert.IsType<BadRequestObjectResult>(
                await CreateController(svcMock.Object, ValidExpiryPayload()).SubscriptionExpired());
        }

        [Fact]
        public async Task SubscriptionExpired_CallsServiceWithCorrectArguments()
        {
            string capturedEmail = string.Empty, capturedOrderId = string.Empty;
            var svcMock = new Mock<IFourthwallApiSubscriptionService>();
            svcMock.Setup(s => s.ProcessSubscriptionCancellationAsync(
                    It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((e, o) => { capturedEmail = e; capturedOrderId = o; })
                .ReturnsAsync(true);

            await CreateController(svcMock.Object,
                ValidExpiryPayload("test@example.com", "exp_order_456")).SubscriptionExpired();

            Assert.Equal("test@example.com", capturedEmail);
            Assert.Equal("exp_order_456", capturedOrderId);
        }

        // ─── SubscriptionChanged ───────────────────────────────────────────────

        [Fact]
        public async Task SubscriptionChanged_ValidPayload_Returns200()
        {
            var svcMock = new Mock<IFourthwallApiSubscriptionService>();
            svcMock.Setup(s => s.ProcessSubscriptionPurchaseAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(true);

            Assert.IsType<OkObjectResult>(
                await CreateController(svcMock.Object, ValidChangedPayload()).SubscriptionChanged());
        }

        [Fact]
        public async Task SubscriptionChanged_WrongEventType_Returns400()
        {
            var payload = ValidChangedPayload();
            payload.Type = "SUBSCRIPTION_EXPIRED";
            Assert.IsType<BadRequestObjectResult>(
                await CreateController(payload: payload).SubscriptionChanged());
        }

        [Fact]
        public async Task SubscriptionChanged_MissingEmail_Returns400()
        {
            var payload = ValidChangedPayload();
            payload.Data!.Email = null;
            Assert.IsType<BadRequestObjectResult>(
                await CreateController(payload: payload).SubscriptionChanged());
        }

        [Fact]
        public async Task SubscriptionChanged_TreatedAsRenewal_CallsPurchaseService()
        {
            var svcMock = new Mock<IFourthwallApiSubscriptionService>();
            svcMock.Setup(s => s.ProcessSubscriptionPurchaseAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(true);

            await CreateController(svcMock.Object,
                ValidChangedPayload("user@test.com", "chg_789")).SubscriptionChanged();

            svcMock.Verify(s => s.ProcessSubscriptionPurchaseAsync(
                "user@test.com", "chg_789", It.IsAny<DateTime>()), Times.Once);
        }

        // ─── Payload fallback ──────────────────────────────────────────────────

        [Fact]
        public async Task SubscriptionActivated_FallsBackToVariantId_WhenDataIdIsNull()
        {
            string capturedOrderId = string.Empty;
            var svcMock = new Mock<IFourthwallApiSubscriptionService>();
            svcMock.Setup(s => s.ProcessSubscriptionPurchaseAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .Callback<string, string, DateTime>((_, o, _) => capturedOrderId = o)
                .ReturnsAsync(true);

            var payload = ValidPurchasePayload();
            payload.Data!.Id = null;
            payload.Data.Subscription!.Variant = new FourthwallSubscriptionVariant { Id = "variant_fallback" };

            await CreateController(svcMock.Object, payload).SubscriptionActivated();

            Assert.Equal("variant_fallback", capturedOrderId);
        }
    }
}
