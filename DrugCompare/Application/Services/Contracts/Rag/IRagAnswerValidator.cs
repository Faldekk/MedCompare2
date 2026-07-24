using DrugCompare.Application.Models.Rag;

namespace DrugCompare.Application.Services.Contracts.Rag;

public interface IRagAnswerValidator
{
    RagAnswerValidationResult Validate(RagAnswer answer, IReadOnlyList<KnowledgeChunkResult> suppliedSources);
}
