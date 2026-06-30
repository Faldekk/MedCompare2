using DrugCompare.Application.Models.KnowledgeBase;

namespace DrugCompare.Application.Repositories.Contracts.KnowledgeBase;

public interface IKnowledgeChunkRepository
{
    Task<long> AddAsync(
        KnowledgeChunk chunk,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<KnowledgeChunk> chunks,
        CancellationToken cancellationToken = default);

    Task<KnowledgeChunk?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeChunk>> SearchFtsAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeChunk>> GetBySourceAsync(
        string sourceType,
        long sourceId,
        CancellationToken cancellationToken = default);
}