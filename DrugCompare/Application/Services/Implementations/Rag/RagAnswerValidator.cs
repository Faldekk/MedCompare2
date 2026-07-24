using DrugCompare.Application.Models.Rag;
using DrugCompare.Application.Services.Contracts.Rag;

namespace DrugCompare.Application.Services.Implementations.Rag;

public sealed class RagAnswerValidator : IRagAnswerValidator
{
    private static readonly HashSet<string> AllowedStatuses = ["reviewed", "verified"];

    public RagAnswerValidationResult Validate(RagAnswer answer, IReadOnlyList<KnowledgeChunkResult> suppliedSources)
    {
        if (string.IsNullOrWhiteSpace(answer.Summary)) return Invalid("Model returned an empty summary.");
        if (answer.InsufficientEvidence)
            return answer.Findings.Count == 0 ? new RagAnswerValidationResult { IsValid = true } : Invalid("An insufficient-evidence response cannot contain findings.");
        if (answer.Findings.Count == 0) return Invalid("Model returned no source-backed findings.");

        var allowedIds = suppliedSources.Where(source => AllowedStatuses.Contains(source.ReviewStatus)).Select(source => source.Id).ToHashSet();
        foreach (var finding in answer.Findings)
        {
            if (string.IsNullOrWhiteSpace(finding.Claim) || finding.SourceIds.Count == 0)
                return Invalid("Every finding requires a claim and at least one source_id.");
            if (finding.SourceIds.Any(id => !allowedIds.Contains(id)))
                return Invalid("Model cited a source that was not supplied as reviewed or verified evidence.");
        }

        return new RagAnswerValidationResult { IsValid = true };
    }

    private static RagAnswerValidationResult Invalid(string error) => new() { IsValid = false, Error = error };
}
