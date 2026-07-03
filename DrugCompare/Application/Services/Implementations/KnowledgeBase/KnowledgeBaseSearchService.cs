using DrugCompare.Application.Models.KnowledgeBase;
using DrugCompare.Application.Repositories.Contracts.KnowledgeBase;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;

namespace DrugCompare.Application.Services.Implementations.KnowledgeBase;

public sealed class KnowledgeBaseSearchService : IKnowledgeBaseSearchService
{
    private readonly IKnowledgeChunkRepository _knowledgeChunkRepository;

    public KnowledgeBaseSearchService(
        IKnowledgeChunkRepository knowledgeChunkRepository)
    {
        _knowledgeChunkRepository = knowledgeChunkRepository;
    }

    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var chunks = await _knowledgeChunkRepository.SearchFtsAsync(
            query,
            limit,
            cancellationToken);

        return chunks
            .Select(chunk => new KnowledgeSearchResult
            {
                Id = chunk.Id,
                SourceType = chunk.SourceType,
                SourceId = chunk.SourceId,
                SourceTitle = chunk.SourceTitle,
                ProductName = chunk.ProductName,
                ActiveSubstance = chunk.ActiveSubstance,
                SectionNumber = chunk.SectionNumber,
                SectionTitle = chunk.SectionTitle,
                ChunkText = chunk.ChunkText,
                ReviewStatus = chunk.ReviewStatus,
                SourceUrl = chunk.SourceUrl
            })
            .ToList();
    }
}