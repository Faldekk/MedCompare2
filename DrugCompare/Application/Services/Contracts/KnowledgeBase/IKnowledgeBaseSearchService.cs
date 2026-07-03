using DrugCompare.Application.Models.KnowledgeBase;

namespace DrugCompare.Application.Services.Contracts.KnowledgeBase;

public interface IKnowledgeBaseSearchService
{
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default);
}