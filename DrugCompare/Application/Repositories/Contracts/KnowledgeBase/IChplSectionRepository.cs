using DrugCompare.Application.Models.KnowledgeBase;

namespace DrugCompare.Application.Repositories.Contracts.KnowledgeBase;

public interface IChplSectionRepository
{
    Task<long> AddAsync(
        ChplSectionRecord section,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<ChplSectionRecord> sections,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChplSectionRecord>> GetByDocumentIdAsync(
        long chplDocumentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChplSectionRecord>> GetBySectionNumberAsync(
        string sectionNumber,
        int limit = 100,
        CancellationToken cancellationToken = default);
}