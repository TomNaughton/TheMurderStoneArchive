namespace TheMurderStoneArchive.Services
{
    public interface IPdfDocumentService
    {
        Task<byte[]> GenerateProjectBriefPdfAsync(CancellationToken cancellationToken = default);

        Task<byte[]> GenerateResearchPackOverviewPdfAsync(CancellationToken cancellationToken = default);

        Task<byte[]> GenerateResearchPackTimelinePdfAsync(CancellationToken cancellationToken = default);

        Task<byte[]> GenerateResearchPackNotesPdfAsync(CancellationToken cancellationToken = default);
    }
}
