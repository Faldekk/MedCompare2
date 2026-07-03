using DrugCompare.Application.Models.Rag;

namespace DrugCompare.Application.Services.Contracts.Rag;

public interface IRagRetriever
{
    Task<IReadOnlyList<KnowledgeChunkResult>> RetrieveAsync(
        string query,
        RagRetrievalOptions options,
        CancellationToken cancellationToken = default);
}