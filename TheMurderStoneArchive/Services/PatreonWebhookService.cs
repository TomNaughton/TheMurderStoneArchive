using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Services
{
    public class PatreonWebhookService : IPatreonWebhookService
    {
        private readonly ApplicationDbContext _context;
        private readonly DonationOptions _options;
        private readonly ILogger<PatreonWebhookService> _logger;

        public PatreonWebhookService(
            ApplicationDbContext context,
            IOptions<DonationOptions> options,
            ILogger<PatreonWebhookService> logger)
        {
            _context = context;
            _options = options.Value;
            _logger = logger;
        }

        public async Task HandleWebhookAsync(
            string payload,
            string signatureHeader,
            string? eventType,
            long defaultCampaignId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.PatreonWebhookSecret))
            {
                throw new InvalidOperationException("Patreon webhook secret is not configured.");
            }

            if (!VerifyWebhookSignature(payload, signatureHeader, _options.PatreonWebhookSecret))
            {
                throw new UnauthorizedAccessException("Patreon webhook signature verification failed.");
            }

            var allowedEvents = ParseAllowedEvents(_options.PatreonOneTimeEventTypes);
            if (!string.IsNullOrWhiteSpace(eventType) && allowedEvents.Count > 0 && !allowedEvents.Contains(eventType))
            {
                _logger.LogInformation("Ignoring Patreon webhook event type {EventType}", eventType);
                return;
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var externalPaymentId =
                TryGetStringByPath(root, "data", "id") ??
                TryGetStringByPath(root, "data", "attributes", "charge_id") ??
                TryGetStringByPath(root, "data", "attributes", "payment_id");

            if (string.IsNullOrWhiteSpace(externalPaymentId))
            {
                _logger.LogWarning("Patreon webhook payload missing a usable payment identifier. Keeping last known total.");
                return;
            }

            var amountGbp = ParseAmountGbp(root);
            if (amountGbp <= 0)
            {
                _logger.LogWarning("Patreon webhook payload for {PaymentId} has no positive one-time amount. Keeping last known total.", externalPaymentId);
                return;
            }

            var contributorName =
                TryGetStringByPath(root, "data", "attributes", "full_name") ??
                TryGetStringByPath(root, "data", "attributes", "name");

            var contributorEmail = TryGetStringByPath(root, "data", "attributes", "email");
            var note = TryGetStringByPath(root, "data", "attributes", "note") ?? "Patreon one-time purchase";

            var contribution = await _context.MonetaryContributions
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync(c => c.Source == "Patreon" && c.ProviderPaymentIntentId == externalPaymentId, cancellationToken);

            if (contribution == null)
            {
                contribution = new MonetaryContribution
                {
                    DonationCampaignId = defaultCampaignId,
                    AmountGbp = amountGbp,
                    Currency = "GBP",
                    Source = "Patreon",
                    ProviderPaymentIntentId = externalPaymentId,
                    ProviderChargeId = eventType,
                    ContributorName = contributorName,
                    ContributorEmail = contributorEmail,
                    Note = note,
                    Status = "Paid",
                    IsCountedInTotal = true,
                    SubmittedAtUtc = DateTime.UtcNow,
                    ReceivedAtUtc = DateTime.UtcNow
                };

                _context.MonetaryContributions.Add(contribution);
            }
            else
            {
                contribution.AmountGbp = amountGbp;
                contribution.ProviderChargeId = eventType;
                contribution.ContributorName = contributorName;
                contribution.ContributorEmail = contributorEmail;
                contribution.Note = note;
                contribution.Status = "Paid";
                contribution.ReceivedAtUtc = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Patreon webhook processed for payment {PaymentId}", externalPaymentId);
        }

        private static HashSet<string> ParseAllowedEvents(string raw)
        {
            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static decimal ParseAmountGbp(JsonElement root)
        {
            var amountCents =
                TryGetLongByPath(root, "data", "attributes", "amount_cents") ??
                TryGetLongByPath(root, "data", "attributes", "last_charge_amount_cents") ??
                TryGetLongByPath(root, "data", "attributes", "currently_entitled_amount_cents") ??
                TryGetLongByPath(root, "data", "attributes", "pledge_amount_cents");

            if (amountCents.HasValue)
            {
                return Math.Round(amountCents.Value / 100m, 2, MidpointRounding.AwayFromZero);
            }

            var amountString =
                TryGetStringByPath(root, "data", "attributes", "amount") ??
                TryGetStringByPath(root, "data", "attributes", "last_charge_amount");

            if (!string.IsNullOrWhiteSpace(amountString) && decimal.TryParse(amountString, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            }

            return 0m;
        }

        private static string? TryGetStringByPath(JsonElement element, params string[] path)
        {
            var target = TryGetByPath(element, path);
            return target.HasValue && target.Value.ValueKind == JsonValueKind.String
                ? target.Value.GetString()
                : null;
        }

        private static long? TryGetLongByPath(JsonElement element, params string[] path)
        {
            var target = TryGetByPath(element, path);
            if (!target.HasValue)
            {
                return null;
            }

            if (target.Value.ValueKind == JsonValueKind.Number && target.Value.TryGetInt64(out var numeric))
            {
                return numeric;
            }

            if (target.Value.ValueKind == JsonValueKind.String && long.TryParse(target.Value.GetString(), out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static JsonElement? TryGetByPath(JsonElement element, params string[] path)
        {
            var current = element;
            foreach (var segment in path)
            {
                if (!current.TryGetProperty(segment, out current))
                {
                    return null;
                }
            }

            return current;
        }

        private static bool VerifyWebhookSignature(string payload, string signatureHeader, string webhookSecret)
        {
            if (string.IsNullOrWhiteSpace(signatureHeader))
            {
                return false;
            }

            using var hmac = new HMACMD5(Encoding.UTF8.GetBytes(webhookSecret));
            var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expectedSignature = Convert.ToHexString(signatureBytes).ToLowerInvariant();

            var providedBytes = Encoding.UTF8.GetBytes(signatureHeader.Trim());
            var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);

            return providedBytes.Length == expectedBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }
    }
}
