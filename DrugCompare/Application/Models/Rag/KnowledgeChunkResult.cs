namespace DrugCompare.Application.Models.Rag;

public sealed class KnowledgeChunkResult
{
    public long Id { get; set; }

    public string SourceType { get; set; } = string.Empty;
    public long? SourceId { get; set; }
    public long? ParentSectionId { get; set; }
    public int ChunkIndex { get; set; }

    public string SourceTitle { get; set; } = string.Empty;

    public string? ProductName { get; set; }
    public string? ActiveSubstance { get; set; }

    public string? SectionNumber { get; set; }
    public string? SectionTitle { get; set; }

    public string ChunkText { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "needs_review";
    public string? ClinicalCategory { get; set; }
    public int ClinicalPriority { get; set; }
    public string EvidenceKind { get; set; } = "unknown";

    public string? SourceUrl { get; set; }
}
