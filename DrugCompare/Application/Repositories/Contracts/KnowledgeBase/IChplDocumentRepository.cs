using DrugCompare.Application.Models.KnowledgeBase;

namespace DrugCompare.Application.Repositories.Contracts.KnowledgeBase;

public interface IChplDocumentRepository
{
    Task<long> AddAsync(
        ChplDocumentRecord document,
        CancellationToken cancellationToken = default);

    Task<ChplDocumentRecord?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<ChplDocumentRecord?> GetByFileHashAsync(
        string fileHash,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChplDocumentRecord>> SearchByProductNameAsync(
        string productName,
        int limit = 50,
        CancellationToken cancellationToken = default);
}