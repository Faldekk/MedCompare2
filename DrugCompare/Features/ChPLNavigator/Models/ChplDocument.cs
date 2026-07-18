namespace DrugCompare.Features.ChPLNavigator.Models;

public sealed class ChplDocument
{
    public long? RplProductId { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "ChPL";
    public string Language { get; set; } = "pl";
    public string? ProductName { get; set; }
    public string? ActiveSubstanceText { get; set; }
    public string? ChplUrl { get; set; }
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;

    public List<ChplSection> Sections { get; set; } = new();
}
