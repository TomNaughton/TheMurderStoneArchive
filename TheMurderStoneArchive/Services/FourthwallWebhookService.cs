using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Helpers;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Services
{
    public class FourthwallWebhookService : IFourthwallWebhookService
    {
        private readonly ApplicationDbContext _context;
        private readonly DonationOptions _options;
        private readonly ILogger<FourthwallWebhookService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;

        public FourthwallWebhookService(
            ApplicationDbContext context,
            IOptions<DonationOptions> options,
            ILogger<FourthwallWebhookService> logger,
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache)
        {
            _context = context;
            _options = options.Value;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
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

            if (string.IsNullOrWhiteSpace(_options.FourthwallWebhookSecret))
            {
                throw new InvalidOperationException("Fourthwall webhook secret is not configured.");
            }

            if (!FourthwallSignatureVerifier.VerifySignature(payload, signatureHeader, _options.FourthwallWebhookSecret))
            {
                throw new UnauthorizedAccessException("Fourthwall webhook signature verification failed.");
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var effectiveEventType = !string.IsNullOrWhiteSpace(eventType)
                ? eventType
                : TryGetStringByPath(root, "type") ?? TryGetStringByPath(root, "event", "type");

            var allowedEvents = ParseAllowedEvents(_options.FourthwallOneTimeEventTypes);
            if (!string.IsNullOrWhiteSpace(effectiveEventType) && allowedEvents.Count > 0 && !allowedEvents.Contains(effectiveEventType))
            {
                _logger.LogInformation("Ignoring Fourthwall webhook event type {EventType}", effectiveEventType);
                return;
            }

            var externalPaymentId =
                TryGetStringByPath(root, "data", "id") ??
                TryGetStringByPath(root, "payment", "id") ??
                TryGetStringByPath(root, "order", "id") ??
                TryGetStringByPath(root, "checkout", "id") ??
                TryGetStringByPath(root, "event", "id") ??
                TryGetStringByPath(root, "id");

            if (string.IsNullOrWhiteSpace(externalPaymentId))
            {
                _logger.LogWarning("Fourthwall webhook payload missing a usable payment identifier. Keeping last known total.");
                return;
            }

            var sourceAmount = ParseAmountGbp(root);
            if (sourceAmount <= 0)
            {
                _logger.LogWarning("Fourthwall webhook payload for {PaymentId} has no positive one-time amount. Keeping last known total.", externalPaymentId);
                return;
            }

            var sourceCurrency = ParseCurrency(root).ToUpperInvariant();
            var amountGbp = sourceAmount;

            if (!string.Equals(sourceCurrency, "GBP", StringComparison.OrdinalIgnoreCase))
            {
                var convertedAmount = await TryConvertToGbpAsync(sourceAmount, sourceCurrency, cancellationToken);
                if (!convertedAmount.HasValue || convertedAmount.Value <= 0)
                {
                    _logger.LogWarning("Fourthwall payment {PaymentId} currency conversion failed from {Currency} to GBP. Keeping last known total.", externalPaymentId, sourceCurrency);
                    return;
                }

                amountGbp = convertedAmount.Value;
                _logger.LogInformation("Fourthwall payment {PaymentId} converted from {SourceAmount} {SourceCurrency} to {AmountGbp} GBP.", externalPaymentId, sourceAmount, sourceCurrency, amountGbp);
            }

            var contributorName =
                TryGetStringByPath(root, "data", "username") ??
                TryGetStringByPath(root, "customer", "name") ??
                TryGetStringByPath(root, "data", "customer", "name") ??
                TryGetStringByPath(root, "data", "attributes", "name");

            var contributorEmail =
                TryGetStringByPath(root, "data", "email") ??
                TryGetStringByPath(root, "customer", "email") ??
                TryGetStringByPath(root, "data", "customer", "email") ??
                TryGetStringByPath(root, "data", "attributes", "email");

            var note = TryGetStringByPath(root, "data", "message") ?? "Fourthwall one-time purchase";
            if (!string.Equals(sourceCurrency, "GBP", StringComparison.OrdinalIgnoreCase))
            {
                note = $"{note} (converted from {sourceAmount.ToString("0.00", CultureInfo.InvariantCulture)} {sourceCurrency})";
            }

            var contribution = await _context.MonetaryContributions
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync(c => c.Source == "Fourthwall" && c.ProviderPaymentIntentId == externalPaymentId, cancellationToken);

            if (contribution == null)
            {
                contribution = new MonetaryContribution
                {
                    DonationCampaignId = defaultCampaignId,
                    AmountGbp = amountGbp,
                    Currency = "GBP",
                    Source = "Fourthwall",
                    ProviderPaymentIntentId = externalPaymentId,
                    ProviderChargeId = effectiveEventType,
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
                contribution.Currency = "GBP";
                contribution.ProviderChargeId = effectiveEventType;
                contribution.ContributorName = contributorName;
                contribution.ContributorEmail = contributorEmail;
                contribution.Note = note;
                contribution.Status = "Paid";
                contribution.ReceivedAtUtc = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Fourthwall webhook processed for payment {PaymentId}", externalPaymentId);
        }

        private static HashSet<string> ParseAllowedEvents(string raw)
        {
            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static decimal ParseAmountGbp(JsonElement root)
        {
            var explicitAmount =
                TryGetDecimalByPath(root, "data", "amounts", "total", "value") ??
                TryGetDecimalByPath(root, "amount", "value") ??
                TryGetDecimalByPath(root, "data", "amount") ??
                TryGetDecimalByPath(root, "data", "attributes", "amount") ??
                TryGetDecimalByPath(root, "order", "total", "value") ??
                TryGetDecimalByPath(root, "checkout", "total", "value");

            if (explicitAmount.HasValue)
            {
                return Math.Round(explicitAmount.Value, 2, MidpointRounding.AwayFromZero);
            }

            var amountCents =
                TryGetLongByPath(root, "amount_cents") ??
                TryGetLongByPath(root, "total", "amount") ??
                TryGetLongByPath(root, "data", "amount_cents") ??
                TryGetLongByPath(root, "data", "attributes", "amount_cents") ??
                TryGetLongByPath(root, "order", "total", "amount") ??
                TryGetLongByPath(root, "checkout", "total", "amount");

            if (amountCents.HasValue)
            {
                return Math.Round(amountCents.Value / 100m, 2, MidpointRounding.AwayFromZero);
            }

            var amountString =
                TryGetStringByPath(root, "amount") ??
                TryGetStringByPath(root, "data", "amount") ??
                TryGetStringByPath(root, "data", "attributes", "amount") ??
                TryGetStringByPath(root, "order", "total", "formatted") ??
                TryGetStringByPath(root, "checkout", "total", "formatted");

            if (!string.IsNullOrWhiteSpace(amountString))
            {
                var normalized = new string(amountString.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
                if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                {
                    return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
                }
            }

            return 0m;
        }

        private static string ParseCurrency(JsonElement root)
        {
            return TryGetStringByPath(root, "data", "amounts", "total", "currency") ??
                   TryGetStringByPath(root, "data", "currency") ??
                   TryGetStringByPath(root, "currency") ??
                   "GBP";
        }

        private async Task<decimal?> TryConvertToGbpAsync(decimal amount, string sourceCurrency, CancellationToken cancellationToken)
        {
            var exchangeRate = await GetExchangeRateToGbpAsync(sourceCurrency, cancellationToken);
            if (!exchangeRate.HasValue || exchangeRate.Value <= 0)
            {
                return null;
            }

            return Math.Round(amount * exchangeRate.Value, 2, MidpointRounding.AwayFromZero);
        }

        private async Task<decimal?> GetExchangeRateToGbpAsync(string sourceCurrency, CancellationToken cancellationToken)
        {
            var cacheKey = $"fx:{sourceCurrency}:GBP";
            if (_memoryCache.TryGetValue(cacheKey, out decimal cachedRate) && cachedRate > 0)
            {
                return cachedRate;
            }

            var baseUrl = string.IsNullOrWhiteSpace(_options.ExchangeRateApiBaseUrl)
                ? "https://api.exchangerate.host"
                : _options.ExchangeRateApiBaseUrl.TrimEnd('/');

            var requestUrl = $"{baseUrl}/convert?from={Uri.EscapeDataString(sourceCurrency)}&to=GBP&amount=1";

            try
            {
                var client = _httpClientFactory.CreateClient();
                decimal? rate = null;
                string errorType = "unknown";
                string errorInfo = "no details";

                using var response = await client.GetAsync(requestUrl, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Exchange rate lookup failed for {Currency} to GBP with status {StatusCode}.", sourceCurrency, response.StatusCode);
                }

                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    rate = TryGetDecimalByPath(root, "result") ??
                           TryGetDecimalByPath(root, "info", "rate") ??
                           TryGetDecimalByPath(root, "rates", "GBP") ??
                           TryGetDecimalByPath(root, "quotes", $"{sourceCurrency}GBP");

                    errorType = TryGetStringByPath(root, "error", "type") ?? TryGetStringByPath(root, "error", "code") ?? errorType;
                    errorInfo = TryGetStringByPath(root, "error", "info") ?? TryGetStringByPath(root, "error", "message") ?? errorInfo;
                }

                if (!rate.HasValue || rate.Value <= 0)
                {
                    var latestUrl = $"{baseUrl}/latest?base={Uri.EscapeDataString(sourceCurrency)}&symbols=GBP";
                    using var latestResponse = await client.GetAsync(latestUrl, cancellationToken);
                    var latestJson = await latestResponse.Content.ReadAsStringAsync(cancellationToken);

                    if (latestResponse.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(latestJson))
                    {
                        using var latestDoc = JsonDocument.Parse(latestJson);
                        rate = TryGetDecimalByPath(latestDoc.RootElement, "rates", "GBP") ??
                               TryGetDecimalByPath(latestDoc.RootElement, "quotes", $"{sourceCurrency}GBP");

                        errorType = TryGetStringByPath(latestDoc.RootElement, "error", "type") ?? TryGetStringByPath(latestDoc.RootElement, "error", "code") ?? errorType;
                        errorInfo = TryGetStringByPath(latestDoc.RootElement, "error", "info") ?? TryGetStringByPath(latestDoc.RootElement, "error", "message") ?? errorInfo;
                    }
                }

                if ((!rate.HasValue || rate.Value <= 0) && string.Equals(errorType, "missing_access_key", StringComparison.OrdinalIgnoreCase))
                {
                    var frankfurterUrl = $"https://api.frankfurter.app/latest?from={Uri.EscapeDataString(sourceCurrency)}&to=GBP";
                    using var frankfurterResponse = await client.GetAsync(frankfurterUrl, cancellationToken);
                    var frankfurterJson = await frankfurterResponse.Content.ReadAsStringAsync(cancellationToken);

                    if (frankfurterResponse.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(frankfurterJson))
                    {
                        using var frankfurterDoc = JsonDocument.Parse(frankfurterJson);
                        rate = TryGetDecimalByPath(frankfurterDoc.RootElement, "rates", "GBP");
                    }
                }

                if (!rate.HasValue || rate.Value <= 0)
                {
                    _logger.LogWarning("Exchange rate lookup returned no valid rate for {Currency} to GBP. ErrorType={ErrorType}; ErrorInfo={ErrorInfo}", sourceCurrency, errorType, errorInfo);
                    return null;
                }

                var cacheMinutes = _options.ExchangeRateCacheMinutes <= 0 ? 360 : _options.ExchangeRateCacheMinutes;
                _memoryCache.Set(cacheKey, rate.Value, TimeSpan.FromMinutes(cacheMinutes));

                return rate.Value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exchange rate lookup failed for {Currency} to GBP.", sourceCurrency);
                return null;
            }
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

        private static decimal? TryGetDecimalByPath(JsonElement element, params string[] path)
        {
            var target = TryGetByPath(element, path);
            if (!target.HasValue)
            {
                return null;
            }

            if (target.Value.ValueKind == JsonValueKind.Number && target.Value.TryGetDecimal(out var numeric))
            {
                return numeric;
            }

            if (target.Value.ValueKind == JsonValueKind.String && decimal.TryParse(target.Value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
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

        // Signature verification delegated to FourthwallSignatureVerifier (Helpers/FourthwallSignatureVerifier.cs)
    }
}
