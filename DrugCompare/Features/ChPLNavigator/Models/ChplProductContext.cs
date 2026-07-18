namespace DrugCompare.Features.ChPLNavigator.Models;

public sealed class ChplProductContext
{
    public long RplProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? ActiveSubstanceText { get; init; }
    public string? Strength { get; init; }
    public string? PharmaceuticalForm { get; init; }
    public string? ChplUrl { get; init; }
}
