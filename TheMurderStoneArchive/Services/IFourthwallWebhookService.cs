namespace TheMurderStoneArchive.Services
{
    public interface IFourthwallWebhookService
    {
        Task HandleWebhookAsync(
            string payload,
            string signatureHeader,
            string? eventType,
            long defaultCampaignId,
            CancellationToken cancellationToken = default);
    }
}
