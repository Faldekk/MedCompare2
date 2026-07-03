namespace DrugCompare.Application.Models.KnowledgeBase;

public sealed class KnowledgeBaseStats
{
    public int ChplDocumentCount { get; set; }
    public int ChplSectionCount { get; set; }
    public int KnowledgeChunkCount { get; set; }
    public int NeedsReviewChunkCount { get; set; }
    public int ReviewedChunkCount { get; set; }
    public int VerifiedChunkCount { get; set; }
}