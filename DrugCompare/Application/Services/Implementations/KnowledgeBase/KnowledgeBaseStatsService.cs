using DrugCompare.Application.Models.KnowledgeBase;
using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;

namespace DrugCompare.Application.Services.Implementations.KnowledgeBase;

public sealed class KnowledgeBaseStatsService : IKnowledgeBaseStatsService
{
    private readonly IKnowledgeChunkRepository _knowledgeChunkRepository;

    public KnowledgeBaseStatsService(
        IKnowledgeChunkRepository knowledgeChunkRepository)
    {
        _knowledgeChunkRepository = knowledgeChunkRepository;
    }

    public Task<KnowledgeBaseStats> GetStatsAsync(
        CancellationToken cancellationToken = default)
    {
        return _knowledgeChunkRepository.GetStatsAsync(cancellationToken);
    }
}