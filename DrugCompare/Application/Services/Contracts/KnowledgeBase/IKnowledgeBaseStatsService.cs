using DrugCompare.Application.Models.KnowledgeBase;

namespace DrugCompare.Application.Services.Contracts.KnowledgeBase;

public interface IKnowledgeBaseStatsService
{
    Task<KnowledgeBaseStats> GetStatsAsync(
        CancellationToken cancellationToken = default);
}