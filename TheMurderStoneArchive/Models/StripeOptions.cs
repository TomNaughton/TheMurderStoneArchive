namespace TheMurderStoneArchive.Models
{
    public class StripeOptions
    {
        public string SecretKey { get; set; } = string.Empty;

        public string PublishableKey { get; set; } = string.Empty;

        public string WebhookSecret { get; set; } = string.Empty;

        public string ProductTaxCode { get; set; } = string.Empty;

        public string DefaultSuccessPath { get; set; } = "/Home/Donate?success=1";

        public string DefaultCancelPath { get; set; } = "/Home/Donate?canceled=1";
    }
}
