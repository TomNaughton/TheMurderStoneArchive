using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Services
{
    public class StripePaymentService : IStripePaymentService
    {
        private readonly ApplicationDbContext _context;
        private readonly StripeOptions _options;
        private readonly ILogger<StripePaymentService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public StripePaymentService(
            ApplicationDbContext context,
            IOptions<StripeOptions> options,
            ILogger<StripePaymentService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _options = options.Value;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> CreateCheckoutSessionUrlAsync(
            decimal amountGbp,
            string description,
            string successUrl,
            string cancelUrl,
            long? campaignId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Stripe checkout config state: SecretKeySet={SecretKeySet}, PublishableKeySet={PublishableKeySet}, WebhookSecretSet={WebhookSecretSet}, ProductTaxCodeSet={ProductTaxCodeSet}",
                !string.IsNullOrWhiteSpace(_options.SecretKey),
                !string.IsNullOrWhiteSpace(_options.PublishableKey),
                !string.IsNullOrWhiteSpace(_options.WebhookSecret),
                !string.IsNullOrWhiteSpace(_options.ProductTaxCode));

            if (string.IsNullOrWhiteSpace(_options.SecretKey))
            {
                throw new InvalidOperationException("Stripe secret key is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_options.ProductTaxCode))
            {
                throw new InvalidOperationException("Stripe product tax code is not configured.");
            }

            var unitAmountPence = Convert.ToInt64(Math.Round(amountGbp * 100m, MidpointRounding.AwayFromZero));
            if (unitAmountPence <= 0)
            {
                throw new InvalidOperationException("Donation amount must be greater than zero.");
            }

            var requestData = new Dictionary<string, string>
            {
                ["mode"] = "payment",
                ["success_url"] = successUrl,
                ["cancel_url"] = cancelUrl,
                ["line_items[0][quantity]"] = "1",
                ["line_items[0][price_data][currency]"] = "gbp",
                ["line_items[0][price_data][unit_amount]"] = unitAmountPence.ToString(),
                ["line_items[0][price_data][product_data][name]"] = "The Murder Stone Archive Contribution",
                ["line_items[0][price_data][product_data][description]"] = description,
                ["line_items[0][price_data][product_data][tax_code]"] = _options.ProductTaxCode,
                ["metadata[campaignId]"] = campaignId?.ToString() ?? string.Empty
            };

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

            using var content = new FormUrlEncodedContent(requestData);
            using var response = await httpClient.PostAsync("https://api.stripe.com/v1/checkout/sessions", content, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Stripe checkout session creation failed. Status={StatusCode}; Body={Body}", response.StatusCode, payload);
                throw new InvalidOperationException("Unable to create Stripe checkout session.");
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var sessionId = root.GetProperty("id").GetString();
            var sessionUrl = root.GetProperty("url").GetString();

            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(sessionUrl))
            {
                throw new InvalidOperationException("Stripe checkout session response was incomplete.");
            }

            _context.MonetaryContributions.Add(new MonetaryContribution
            {
                DonationCampaignId = campaignId,
                AmountGbp = amountGbp,
                Currency = "GBP",
                Source = "Stripe",
                ProviderSessionId = sessionId,
                Status = "CheckoutCreated",
                SubmittedAtUtc = DateTime.UtcNow,
                IsCountedInTotal = true
            });

            await _context.SaveChangesAsync(cancellationToken);

            return sessionUrl;
        }

        public async Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
            {
                throw new InvalidOperationException("Stripe webhook secret is not configured.");
            }

            if (!VerifyWebhookSignature(payload, signatureHeader, _options.WebhookSecret))
            {
                throw new UnauthorizedAccessException("Stripe webhook signature verification failed.");
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;

            if (!string.Equals(eventType, "checkout.session.completed", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!root.TryGetProperty("data", out var dataElement) ||
                !dataElement.TryGetProperty("object", out var objectElement))
            {
                return;
            }

            var sessionId = TryGetString(objectElement, "id");
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            var amountTotalPence = TryGetLong(objectElement, "amount_total") ?? 0;
            var paymentIntentId = TryGetString(objectElement, "payment_intent");

            var customerDetails = objectElement.TryGetProperty("customer_details", out var customerDetailsElement)
                ? customerDetailsElement
                : default;
            var email = customerDetails.ValueKind != JsonValueKind.Undefined ? TryGetString(customerDetails, "email") : null;
            var name = customerDetails.ValueKind != JsonValueKind.Undefined ? TryGetString(customerDetails, "name") : null;

            var metadata = objectElement.TryGetProperty("metadata", out var metadataElement)
                ? metadataElement
                : default;
            var campaignId = metadata.ValueKind != JsonValueKind.Undefined
                ? TryParseCampaignId(TryGetString(metadata, "campaignId"))
                : null;

            var contribution = await _context.MonetaryContributions
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(x => x.ProviderSessionId == sessionId, cancellationToken);

            if (contribution == null)
            {
                contribution = new MonetaryContribution
                {
                    DonationCampaignId = campaignId,
                    AmountGbp = amountTotalPence / 100m,
                    Currency = "GBP",
                    Source = "Stripe",
                    ProviderSessionId = sessionId,
                    ProviderPaymentIntentId = paymentIntentId,
                    ContributorEmail = email,
                    ContributorName = name,
                    Status = "Paid",
                    IsCountedInTotal = true,
                    SubmittedAtUtc = DateTime.UtcNow,
                    ReceivedAtUtc = DateTime.UtcNow
                };

                _context.MonetaryContributions.Add(contribution);
            }
            else
            {
                contribution.ProviderPaymentIntentId = paymentIntentId;
                contribution.ContributorEmail = email;
                contribution.ContributorName = name;
                contribution.Status = "Paid";
                contribution.ReceivedAtUtc = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Stripe webhook processed: {EventType}", eventType);
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
        }

        private static long? TryGetLong(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop))
            {
                return null;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var value))
            {
                return value;
            }

            return null;
        }

        private static long? TryParseCampaignId(string? value)
        {
            return long.TryParse(value, out var parsed) ? parsed : null;
        }

        private static bool VerifyWebhookSignature(string payload, string signatureHeader, string webhookSecret)
        {
            if (string.IsNullOrWhiteSpace(signatureHeader))
            {
                return false;
            }

            var parts = signatureHeader
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
                .Where(part => part.Length == 2)
                .ToDictionary(part => part[0], part => part[1], StringComparer.OrdinalIgnoreCase);

            if (!parts.TryGetValue("t", out var timestamp) || !parts.TryGetValue("v1", out var signature))
            {
                return false;
            }

            if (!long.TryParse(timestamp, out var timestampSeconds))
            {
                return false;
            }

            var timestampUtc = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
            var age = DateTimeOffset.UtcNow - timestampUtc;
            if (age.Duration() > TimeSpan.FromMinutes(5))
            {
                return false;
            }

            var signedPayload = $"{timestamp}.{payload}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
            var expectedSignature = Convert.ToHexString(hashBytes).ToLowerInvariant();

            var providedBytes = Encoding.UTF8.GetBytes(signature);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);

            return providedBytes.Length == expectedBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }
    }
}
