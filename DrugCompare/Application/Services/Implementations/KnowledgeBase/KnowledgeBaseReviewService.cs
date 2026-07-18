using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;

namespace DrugCompare.Application.Services.Implementations.KnowledgeBase;

public sealed class KnowledgeBaseReviewService : IKnowledgeBaseReviewService
{
    private static readonly HashSet<string> AllowedStatuses = ["reviewed", "verified", "rejected", "deprecated"];
    private readonly IKnowledgeBaseReviewRepository _repository;

    public KnowledgeBaseReviewService(IKnowledgeBaseReviewRepository repository) => _repository = repository;

    public Task ReviewDocumentForChunkAsync(long chunkId, string newStatus, string reviewedBy, string? reviewNote = null, CancellationToken cancellationToken = default)
    {
        if (chunkId <= 0) throw new ArgumentOutOfRangeException(nameof(chunkId));
        if (!AllowedStatuses.Contains(newStatus)) throw new ArgumentException("Nieprawidłowy status review.", nameof(newStatus));
        if (string.IsNullOrWhiteSpace(reviewedBy)) throw new ArgumentException("Podaj osobę weryfikującą.", nameof(reviewedBy));
        return _repository.ReviewDocumentForChunkAsync(chunkId, newStatus, reviewedBy.Trim(), reviewNote?.Trim(), cancellationToken);
    }
}
