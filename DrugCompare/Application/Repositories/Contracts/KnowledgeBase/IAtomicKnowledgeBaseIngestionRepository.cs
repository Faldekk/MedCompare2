using DrugCompare.Application.Models.KnowledgeBase;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;

namespace DrugCompare.Application.Repositories.Contracts.KnowledgeBase;

/// <summary>
/// Persists one parsed ChPL document together with its sections and search chunks.
/// The implementation must make the whole operation atomic.
/// </summary>
public interface IAtomicKnowledgeBaseIngestionRepository
{
    Task<KnowledgeBaseIngestionResult> IngestAsync(
        ChplDocumentRecord document,
        IReadOnlyCollection<ChplSectionRecord> sections,
        CancellationToken cancellationToken = default);
}
