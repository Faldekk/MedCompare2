namespace DrugCompare.Application.Services.Contracts.KnowledgeBase;

public sealed class KnowledgeBaseIngestion
{
    public long ChplDocumentId { get; init; }
    public int SectionsSaved { get; init; }
    public int ChunksSaved { get; init; }

    public string ReviewStatus { get; init; } = "needs_review";
}