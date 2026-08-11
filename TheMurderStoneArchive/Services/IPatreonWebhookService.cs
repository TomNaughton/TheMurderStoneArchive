namespace TheMurderStoneArchive.Services
{
    public interface IPatreonWebhookService
    {
        Task HandleWebhookAsync(
            string payload,
            string signatureHeader,
            string? eventType,
            long defaultCampaignId,
            CancellationToken cancellationToken = default);
    }
}
