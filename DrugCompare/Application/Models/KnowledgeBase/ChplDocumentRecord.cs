namespace DrugCompare.Application.Models.KnowledgeBase;

public sealed class ChplDocumentRecord
{
    public long Id { get; set; }
    public long? RplProductId { get; set; }

    public string? ProductName { get; set; }
    public string? ActiveSubstanceText { get; set; }
    public string? ChplUrl { get; set; }

    public string? SourceFile { get; set; }
    public string? LocalFilePath { get; set; }
    public string? FileHash { get; set; }

    public string DocumentType { get; set; } = "ChPL";
    public string Language { get; set; } = "pl";

    public string? ParserVersion { get; set; }
    public string ReviewStatus { get; set; } = "needs_review";

    public DateTime ImportedAt { get; set; }
    public DateTime? ParsedAt { get; set; }
}
