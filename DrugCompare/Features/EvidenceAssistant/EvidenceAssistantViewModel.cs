using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Models.Rag;
using DrugCompare.Application.Services.Contracts.Rag;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using System.Diagnostics;
using System.IO;

namespace DrugCompare.Features.EvidenceAssistant;

public sealed partial class EvidenceAssistantViewModel : ObservableObject
{
    private readonly IRagRetriever _ragRetriever;
    private readonly IKnowledgeBaseReviewService _reviewService;
    private readonly IRagAnswerService _ragAnswerService;

    public EvidenceAssistantViewModel(IRagRetriever ragRetriever, IKnowledgeBaseReviewService reviewService, IRagAnswerService ragAnswerService)
    {
        _ragRetriever = ragRetriever;
        _reviewService = reviewService;
        _ragAnswerService = ragAnswerService;
    }

    [ObservableProperty]
    private string query = string.Empty;

    [ObservableProperty]
    private string productName = string.Empty;

    [ObservableProperty]
    private string sectionNumber = string.Empty;

    [ObservableProperty]
    private bool includeNeedsReview;

    [ObservableProperty]
    private string statusMessage = "Gotowe.";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private KnowledgeChunkResult? selectedResult;

    [ObservableProperty]
    private string reviewerName = string.Empty;

    [ObservableProperty]
    private string reviewNote = string.Empty;

    [ObservableProperty]
    private string generatedAnswer = string.Empty;

    public ObservableCollection<KnowledgeChunkResult> Results { get; } = new();

    [RelayCommand]
    private async Task SearchAsync()
    {
        Results.Clear();
        GeneratedAnswer = string.Empty;

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
        GeneratedAnswer = string.Empty;
    }

    [RelayCommand]
    private async Task ChangeReviewStatusAsync(string? status)
    {
        if (SelectedResult is null)
        {
            StatusMessage = "Wybierz źródło do weryfikacji.";
            return;
        }

        try
        {
            IsBusy = true;
            await _reviewService.ReviewDocumentForChunkAsync(SelectedResult.Id, status ?? string.Empty, ReviewerName, ReviewNote);
            StatusMessage = $"Dokument źródłowy oznaczono jako: {status}.";
            await SearchAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nie udało się zmienić statusu: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenSelectedSource()
    {
        if (string.IsNullOrWhiteSpace(SelectedResult?.SourceUrl))
        {
            StatusMessage = "Wybrane źródło nie ma adresu URL.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = SelectedResult.SourceUrl, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nie udało się otworzyć źródła: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GenerateRagAnswerAsync()
    {
        WriteRagUiLog($"Generate requested. Query='{Query}', existing results={Results.Count}.");
        if (string.IsNullOrWhiteSpace(Query))
        {
            GeneratedAnswer = string.Empty;
            StatusMessage = "Wpisz pytanie przed uruchomieniem RAG.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Przygotowywanie lokalnych źródeł dla RAG...";

            if (Results.Count == 0)
            {
                var options = new RagRetrievalOptions
                {
                    Limit = 20,
                    IncludeNeedsReview = IncludeNeedsReview,
                    IncludeReviewed = true,
                    IncludeVerified = true,
                    ProductName = string.IsNullOrWhiteSpace(ProductName) ? null : ProductName,
                    SectionNumber = string.IsNullOrWhiteSpace(SectionNumber) ? null : SectionNumber
                };

                var retrieved = await _ragRetriever.RetrieveAsync(Query, options);
                foreach (var source in retrieved)
                {
                    Results.Add(source);
                }
                WriteRagUiLog($"Automatic retrieval completed. Results={Results.Count}.");
            }

            var approvedSources = Results
                .Where(source => source.ReviewStatus is "reviewed" or "verified")
                .ToList();

            if (approvedSources.Count == 0)
            {
                WriteRagUiLog("No reviewed or verified sources. Ollama call skipped.");
                GeneratedAnswer =
                    "Brak zatwierdzonych źródeł dla tego pytania. " +
                    "Zaimportuj ChPL, wyszukaj je i oznacz dokument jako Reviewed lub Verified. " +
                    "Model Ollama nie został wywołany.";
                StatusMessage = "RAG wymaga co najmniej jednego źródła Reviewed lub Verified.";
                return;
            }

            StatusMessage = $"Redagowanie odpowiedzi z {approvedSources.Count} zatwierdzonych źródeł przez Ollama...";
            WriteRagUiLog($"Ollama call started. Approved sources={approvedSources.Count}.");
            var answer = await _ragAnswerService.GenerateAsync(Query, approvedSources);
            WriteRagUiLog($"Ollama call completed. Findings={answer.Findings.Count}; insufficient={answer.InsufficientEvidence}.");
            GeneratedAnswer = answer.Summary;
            if (answer.Findings.Count > 0)
                GeneratedAnswer += Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, answer.Findings.Select(finding => $"• {finding.Claim} [źródła: {string.Join(", ", finding.SourceIds)}]"));
            StatusMessage = answer.InsufficientEvidence ? "Brak wystarczających zweryfikowanych źródeł." : "Odpowiedź utworzona i zwalidowana względem przekazanych źródeł.";
        }
        catch (Exception ex)
        {
            WriteRagUiLog("Generate failed.", ex);
            GeneratedAnswer = string.Empty;
            StatusMessage = $"Lokalny RAG nie jest dostępny: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void WriteRagUiLog(string message, Exception? exception = null)
    {
        try
        {
            var line = $"[{DateTimeOffset.Now:O}] {message}";
            if (exception is not null) line += Environment.NewLine + exception;
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "rag-ui.log"), line + Environment.NewLine);
        }
        catch
        {
            // Diagnostics must not affect the UI command.
        }
    }
}
