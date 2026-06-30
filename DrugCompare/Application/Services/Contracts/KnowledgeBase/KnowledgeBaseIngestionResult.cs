namespace DrugCompare.Application.Services.Contracts.KnowledgeBase;

public sealed class KnowledgeBaseIngestionResult
{
    public long ChplDocumentId { get; init; }
    public int SectionsSaved { get; init; }
    public int ChunksSaved { get; init; }

    public string ReviewStatus { get; init; } = "needs_review";

    public bool WasAlreadyImported { get; init; }
}