namespace DrugCompare.Application.Models.Rag;

public sealed class RagRetrievalOptions
{
    public int Limit { get; set; } = 10;

    public bool IncludeNeedsReview { get; set; } = false;
    public bool IncludeReviewed { get; set; } = true;
    public bool IncludeVerified { get; set; } = true;

    public string? ProductName { get; set; }
    public string? SectionNumber { get; set; }
}