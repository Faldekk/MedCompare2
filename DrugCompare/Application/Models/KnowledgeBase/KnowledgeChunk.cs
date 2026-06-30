namespace DrugCompare.Application.Models.KnowledgeBase;

public sealed class KnowledgeChunk
{
    public long Id { get; set; }

    public string SourceType { get; set; } = string.Empty;
    public long? SourceId { get; set; }

    public string SourceTitle { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? ActiveSubstance { get; set; }

    public string? SectionNumber { get; set; }
    public string? SectionTitle { get; set; }

    public string ChunkText { get; set; } = string.Empty;
    public string ChunkHash { get; set; } = string.Empty;

    public string ReviewStatus { get; set; } = "needs_review";
    public string? SourceUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}