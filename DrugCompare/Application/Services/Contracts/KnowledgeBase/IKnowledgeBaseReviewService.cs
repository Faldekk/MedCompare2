namespace DrugCompare.Application.Services.Contracts.KnowledgeBase;

public interface IKnowledgeBaseReviewService
{
    Task ReviewDocumentForChunkAsync(
        long chunkId,
        string newStatus,
        string reviewedBy,
        string? reviewNote = null,
        CancellationToken cancellationToken = default);
}
