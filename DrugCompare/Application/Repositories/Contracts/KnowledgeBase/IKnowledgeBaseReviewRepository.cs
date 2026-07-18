namespace DrugCompare.Application.Repositories.Contracts.KnowledgeBase;

public interface IKnowledgeBaseReviewRepository
{
    Task ReviewDocumentForChunkAsync(long chunkId, string newStatus, string reviewedBy, string? reviewNote, CancellationToken cancellationToken = default);
}
