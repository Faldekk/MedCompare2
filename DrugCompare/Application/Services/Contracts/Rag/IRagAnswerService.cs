using DrugCompare.Application.Models.Rag;

namespace DrugCompare.Application.Services.Contracts.Rag;

public interface IRagAnswerService
{
    Task<RagAnswer> GenerateAsync(string question, IReadOnlyList<KnowledgeChunkResult> retrievedSources, CancellationToken cancellationToken = default);
}
