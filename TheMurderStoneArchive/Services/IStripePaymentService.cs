namespace TheMurderStoneArchive.Services
{
    public interface IStripePaymentService
    {
        Task<string> CreateCheckoutSessionUrlAsync(
            decimal amountGbp,
            string description,
            string successUrl,
            string cancelUrl,
            long? campaignId,
            CancellationToken cancellationToken = default);

        Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default);
    }
}
