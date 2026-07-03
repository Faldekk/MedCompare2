using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Models.Rag;
using DrugCompare.Application.Services.Contracts.Rag;

namespace DrugCompare.Features.EvidenceAssistant;

public sealed partial class EvidenceAssistantViewModel : ObservableObject
{
    private readonly IRagRetriever _ragRetriever;

    public EvidenceAssistantViewModel(IRagRetriever ragRetriever)
    {
        _ragRetriever = ragRetriever;
    }

    [ObservableProperty]
    private string query = string.Empty;

    [ObservableProperty]
    private string productName = string.Empty;

    [ObservableProperty]
    private string sectionNumber = string.Empty;

    [ObservableProperty]
    private bool includeNeedsReview = true;

    [ObservableProperty]
    private string statusMessage = "Gotowe.";

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<KnowledgeChunkResult> Results { get; } = new();

    [RelayCommand]
    private async Task SearchAsync()
    {
        Results.Clear();

        if (string.IsNullOrWhiteSpace(Query))
        {
            StatusMessage = "Wpisz pytanie albo frazę do wyszukania.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Wyszukiwanie źródeł w lokalnej bazie...";

            var options = new RagRetrievalOptions
            {
                Limit = 20,
                IncludeNeedsReview = IncludeNeedsReview,
                IncludeReviewed = true,
                IncludeVerified = true,
                ProductName = string.IsNullOrWhiteSpace(ProductName) ? null : ProductName,
                SectionNumber = string.IsNullOrWhiteSpace(SectionNumber) ? null : SectionNumber
            };

            var results = await _ragRetriever.RetrieveAsync(Query, options);

            foreach (var result in results)
            {
                Results.Add(result);
            }

            StatusMessage = results.Count == 0
                ? "Nie znaleziono źródeł. Brak danych nie oznacza bezpieczeństwa."
                : $"Znaleziono źródeł: {results.Count}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd wyszukiwania: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Query = string.Empty;
        ProductName = string.Empty;
        SectionNumber = string.Empty;
        Results.Clear();
        StatusMessage = "Wyczyszczono.";
    }
}