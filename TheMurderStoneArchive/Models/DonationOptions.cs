namespace TheMurderStoneArchive.Models
{
    public class DonationOptions
    {
        public string Provider { get; set; } = "Stripe";

        public string PatreonCampaignUrl { get; set; } = string.Empty;

        public string PatreonOneTimePaymentUrl { get; set; } = string.Empty;

        public string PatreonWebhookSecret { get; set; } = string.Empty;

        public string PatreonOneTimeEventTypes { get; set; } = "members:pledge:create,members:pledge:update";

        public string FourthwallOneTimePaymentUrl { get; set; } = string.Empty;

        public string FourthwallWebhookSecret { get; set; } = string.Empty;

        public string FourthwallOneTimeEventTypes { get; set; } = "DONATION,order.paid,checkout.completed";

        public string ExchangeRateApiBaseUrl { get; set; } = "https://api.exchangerate.host";

        public int ExchangeRateCacheMinutes { get; set; } = 360;

        public bool UsePatreon => string.Equals(Provider, "Patreon", StringComparison.OrdinalIgnoreCase);

        public bool UseFourthwall => string.Equals(Provider, "Fourthwall", StringComparison.OrdinalIgnoreCase);
    }
}
