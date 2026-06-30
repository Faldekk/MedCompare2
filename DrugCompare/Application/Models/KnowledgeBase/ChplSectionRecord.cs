namespace DrugCompare.Application.Models.KnowledgeBase;

public sealed class ChplSectionRecord
{
    public long Id { get; set; }
    public long ChplDocumentId { get; set; }

    public string SectionNumber { get; set; } = string.Empty;
    public string? SectionTitle { get; set; }
    public string? SectionType { get; set; }

    public string Text { get; set; } = string.Empty;
    public string? TextHash { get; set; }

    public string ReviewStatus { get; set; } = "needs_review";
    public DateTime CreatedAt { get; set; }
}