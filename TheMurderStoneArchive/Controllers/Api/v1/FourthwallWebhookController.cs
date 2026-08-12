using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TheMurderStoneArchive.Helpers;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Services;
using System.Text;
using System.Text.Json.Serialization;

namespace TheMurderStoneArchive.Controllers.Api.v1
{
    /// <summary>
    /// Webhook endpoint for Fourthwall subscription events (API premium tier).
    /// Process purchase confirmations, expirations, and modifications.
    /// </summary>
    [ApiController]
    [Route("api/v1/webhooks/fourthwall")]
    public class FourthwallWebhookController : ControllerBase
    {
        private readonly IFourthwallApiSubscriptionService _fourthwallService;
        private readonly DonationOptions _donationOptions;
        private readonly ILogger<FourthwallWebhookController> _logger;

        public FourthwallWebhookController(
            IFourthwallApiSubscriptionService fourthwallService,
            IOptions<DonationOptions> donationOptions,
            ILogger<FourthwallWebhookController> logger)
        {
            _fourthwallService = fourthwallService;
            _donationOptions = donationOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// Receive a Fourthwall subscription purchase event.
        /// </summary>
        [HttpPost("subscription-activated")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SubscriptionActivated()
        {
            var (payload, request) = await ReadAndVerifyAsync();
            if (payload == null || request == null)
                return Unauthorized(new { error = "Invalid or missing webhook signature" });

            if (request.Type != "SUBSCRIPTION_PURCHASED")
            {
                _logger.LogWarning("Fourthwall webhook: wrong event type {Type}", request.Type);
                return BadRequest(new { error = "Invalid request" });
            }

            var data = request.Data;
            if (data == null || string.IsNullOrWhiteSpace(data.Email))
            {
                _logger.LogWarning("Fourthwall webhook: missing required fields in payload");
                return BadRequest(new { error = "Missing required fields" });
            }

            var orderId = data.Id ?? data.Subscription?.Variant?.Id;
            if (string.IsNullOrWhiteSpace(orderId))
            {
                _logger.LogWarning("Fourthwall webhook: missing subscription identifier in payload");
                return BadRequest(new { error = "Missing subscription identifier" });
            }

            var customerEmail = data.Email;

            var expiresAt = CalculateSubscriptionExpiryUtc(data);

            _logger.LogInformation("Processing SUBSCRIPTION_PURCHASED: email={Email}, subscriptionId={OrderId}, subscriptionType={Type}, expiresAt={ExpiresAt:O}",
                customerEmail, orderId, data.Subscription?.Type, expiresAt);

            var success = await _fourthwallService.ProcessSubscriptionPurchaseAsync(
                customerEmail,
                orderId,
                expiresAt);

            if (!success)
            {
                _logger.LogWarning("Fourthwall webhook: failed to process activation for {Email}", customerEmail);
                return BadRequest(new { error = "Failed to process subscription" });
            }

            _logger.LogInformation($"Fourthwall subscription activated for {customerEmail}");
            return Ok(new { success = true });
        }

        /// <summary>
        /// Receive a Fourthwall subscription expiration event.
        /// </summary>
        [HttpPost("subscription-expired")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SubscriptionExpired()
        {
            var (payload, request) = await ReadAndVerifyAsync();
            if (payload == null || request == null)
                return Unauthorized(new { error = "Invalid or missing webhook signature" });

            if (request.Type != "SUBSCRIPTION_EXPIRED")
            {
                _logger.LogWarning("Fourthwall webhook: wrong event type {Type} on expiry endpoint", request.Type);
                return BadRequest(new { error = "Invalid request" });
            }

            var data = request.Data;
            if (data == null || string.IsNullOrWhiteSpace(data.Email))
            {
                _logger.LogWarning("Fourthwall webhook: missing required fields in expiry payload");
                return BadRequest(new { error = "Missing required fields" });
            }

            var orderId = data.Id ?? data.Subscription?.Variant?.Id;
            if (string.IsNullOrWhiteSpace(orderId))
            {
                _logger.LogWarning("Fourthwall webhook: missing subscription identifier in expiry payload");
                return BadRequest(new { error = "Missing subscription identifier" });
            }

            var customerEmail = data.Email;

            _logger.LogInformation("Processing SUBSCRIPTION_EXPIRED: email={Email}, subscriptionId={OrderId}",
                customerEmail, orderId);

            var success = await _fourthwallService.ProcessSubscriptionCancellationAsync(
                customerEmail,
                orderId);

            if (!success)
            {
                _logger.LogWarning("Fourthwall webhook: failed to process expiry for {Email}", customerEmail);
                return BadRequest(new { error = "Failed to process expiry" });
            }

            _logger.LogInformation($"Fourthwall subscription expired for {customerEmail}");
            return Ok(new { success = true });
        }

        /// <summary>
        /// Receive a Fourthwall subscription change event (e.g., plan upgrade/downgrade).
        /// </summary>
        [HttpPost("subscription-changed")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SubscriptionChanged()
        {
            var (payload, request) = await ReadAndVerifyAsync();
            if (payload == null || request == null)
                return Unauthorized(new { error = "Invalid or missing webhook signature" });

            if (request.Type != "SUBSCRIPTION_CHANGED")
            {
                _logger.LogWarning("Fourthwall webhook: wrong event type {Type} on changed endpoint", request.Type);
                return BadRequest(new { error = "Invalid request" });
            }

            var data = request.Data;
            if (data == null || string.IsNullOrWhiteSpace(data.Email))
            {
                _logger.LogWarning("Fourthwall webhook: missing required fields in change payload");
                return BadRequest(new { error = "Missing required fields" });
            }

            var orderId = data.Id ?? data.Subscription?.Variant?.Id;
            if (string.IsNullOrWhiteSpace(orderId))
            {
                _logger.LogWarning("Fourthwall webhook: missing subscription identifier in change payload");
                return BadRequest(new { error = "Missing subscription identifier" });
            }

            var customerEmail = data.Email;

            var expiresAt = CalculateSubscriptionExpiryUtc(data);

            _logger.LogInformation("Processing SUBSCRIPTION_CHANGED: email={Email}, subscriptionId={OrderId}, subscriptionType={Type}, expiresAt={ExpiresAt:O}",
                customerEmail, orderId, data.Subscription?.Type, expiresAt);

            // Treat plan changes as a renewal/update
            var success = await _fourthwallService.ProcessSubscriptionPurchaseAsync(
                customerEmail,
                orderId,
                expiresAt);

            if (!success)
            {
                _logger.LogWarning("Fourthwall webhook: failed to process change for {Email}", customerEmail);
                return BadRequest(new { error = "Failed to process change" });
            }

            _logger.LogInformation("Fourthwall subscription changed for {Email}", customerEmail);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Reads the raw request body, verifies the Fourthwall HMAC signature using
        /// FourthwallApiSubscriptionWebhookSecret, and deserializes the payload.
        /// Returns (null, null) if the secret is not configured or the signature is invalid.
        /// </summary>
        private async Task<(string? RawPayload, FourthwallWebhookPayload? Parsed)> ReadAndVerifyAsync()
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var rawPayload = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            var secret = _donationOptions.FourthwallApiSubscriptionWebhookSecret;
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogError("Fourthwall API subscription webhook secret is not configured. Set Donation__FourthwallApiSubscriptionWebhookSecret.");
                return (null, null);
            }

            // Try each header name Fourthwall uses
            var signature =
                Request.Headers["X-Fourthwall-Hmac-Sha256"].ToString()
                    .NullIfEmpty()
                ?? Request.Headers["X-Fourthwall-Hmac-Apps-SHA256"].ToString().NullIfEmpty()
                ?? Request.Headers["X-Fourthwall-Signature"].ToString().NullIfEmpty()
                ?? Request.Headers["X-Signature"].ToString().NullIfEmpty()
                ?? string.Empty;

            if (!FourthwallSignatureVerifier.VerifySignature(rawPayload, signature, secret))
            {
                _logger.LogWarning("Fourthwall API subscription webhook signature verification failed.");
                return (null, null);
            }

            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<FourthwallWebhookPayload>(rawPayload,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return (rawPayload, parsed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fourthwall webhook: failed to deserialize payload");
                return (null, null);
            }
        }

        private static DateTime CalculateSubscriptionExpiryUtc(FourthwallWebhookData data)
        {
            var baseUtc = data.CreatedAt == default
                ? DateTime.UtcNow
                : DateTime.SpecifyKind(data.CreatedAt, DateTimeKind.Utc);

            var subscriptionType = data.Subscription?.Type?.Trim().ToUpperInvariant();
            return subscriptionType switch
            {
                "ANNUAL" or "YEARLY" or "YEAR" => baseUtc.AddYears(1),
                "MONTHLY" or "MONTH" => baseUtc.AddMonths(1),
                _ => DateTime.UtcNow.AddDays(30)
            };
        }
    }

    /// <summary>
    /// Webhook payload structure from Fourthwall API v1.
    /// Represents the WebhookEvent sent by Fourthwall.
    /// </summary>
    public class FourthwallWebhookPayload
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("webhookId")]
        public string? WebhookId { get; set; }

        [JsonPropertyName("shopId")]
        public string? ShopId { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("apiVersion")]
        public string? ApiVersion { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("testMode")]
        public bool TestMode { get; set; }

        [JsonPropertyName("data")]
        public FourthwallWebhookData? Data { get; set; }
    }

    /// <summary>
    /// Data payload for subscription events.
    /// </summary>
    public class FourthwallWebhookData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("nickname")]
        public string? Nickname { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("subscription")]
        public FourthwallSubscriptionData? Subscription { get; set; }
    }

    /// <summary>
    /// Subscription information within the webhook data.
    /// </summary>
    public class FourthwallSubscriptionData
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("variant")]
        public FourthwallSubscriptionVariant? Variant { get; set; }
    }

    /// <summary>
    /// Subscription variant details (plan, price, tier).
    /// </summary>
    public class FourthwallSubscriptionVariant
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("tierId")]
        public string? TierId { get; set; }

        [JsonPropertyName("amount")]
        public FourthwallAmount? Amount { get; set; }

        [JsonPropertyName("offerId")]
        public string? OfferId { get; set; }
    }

    /// <summary>
    /// Price information for the subscription.
    /// </summary>
    public class FourthwallAmount
    {
        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }
    }

    internal static class StringExtensions
    {
        public static string? NullIfEmpty(this string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
