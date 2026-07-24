using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
using DrugCompare.Application.Models;
using DrugCompare.Features.ChPLNavigator.Models;
using DrugCompare.Features.ChPLNavigator.Services;
using Microsoft.Win32;

namespace DrugCompare.Features.ChPLNavigator;

public sealed partial class ChPLNavigatorViewModel : ObservableObject
{
    private readonly PdfTextExtractor _pdfTextExtractor = new();
    private readonly ChplSectionParser _sectionParser = new();
    private readonly ChplRuleTagger _tagger = new();
    private readonly ChplJsonExporter _jsonExporter = new();
    private readonly ChplCsvExporter _csvExporter = new();

    private readonly IKnowledgeBaseIngestionService _knowledgeBaseIngestionService;
    private readonly ChplPdfDownloader _chplPdfDownloader;

    private ChplDocument? _currentDocument;
    private string? _currentFileHash;

    public ChPLNavigatorViewModel(
        IKnowledgeBaseIngestionService knowledgeBaseIngestionService,
        ChplPdfDownloader chplPdfDownloader)
    {
        _knowledgeBaseIngestionService = knowledgeBaseIngestionService;
        _chplPdfDownloader = chplPdfDownloader;
    }

    [ObservableProperty]
    private string? selectedPdfPath;

    [ObservableProperty]
    private string rawText = string.Empty;

    [ObservableProperty]
    private ChplSection? selectedSection;

    [ObservableProperty]
    private string statusMessage = "Gotowe.";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private ChplProductContext? selectedProductContext;

    public ObservableCollection<ChplSection> Sections { get; } = new();

    public void SetProductContext(PolishDrugRegistryItem product)
    {
        SelectedProductContext = new ChplProductContext
        {
            RplProductId = product.Id,
            ProductName = product.ProductName,
            ActiveSubstanceText = product.ActiveSubstanceText,
            Strength = product.Strength,
            PharmaceuticalForm = product.PharmaceuticalForm,
            ChplUrl = product.ChplUrl
        };

        StatusMessage = string.IsNullOrWhiteSpace(product.ChplUrl)
            ? $"Wybrano produkt RPL: {product.ProductName}. Wybierz lokalny plik ChPL PDF."
            : $"Wybrano produkt RPL: {product.ProductName}. Możesz pobrać ChPL z linku lub wybrać lokalny PDF.";
    }

    [RelayCommand]
    private async Task DownloadAndImportChplAsync()
    {
        ChplImportDiagnostics.Write("Automatic ChPL import requested.");
        var context = SelectedProductContext;
        if (context is null)
        {
            StatusMessage = "Najpierw wybierz produkt w Polish Registry i przejdź do ChPL Navigator.";
            return;
        }

        if (string.IsNullOrWhiteSpace(context.ChplUrl))
        {
            StatusMessage = "Wybrany produkt nie ma linku ChPL. Wybierz lokalny plik PDF.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Pobieranie ChPL PDF z linku produktu...";
            var downloaded = await _chplPdfDownloader.DownloadAsync(context.ChplUrl, context.RplProductId);
            SelectedPdfPath = downloaded.LocalFilePath;
            ChplImportDiagnostics.Write($"PDF assigned to navigator: {SelectedPdfPath}");

            await ExtractAndParseAsync();
            ChplImportDiagnostics.Write($"PDF parsing completed. Sections: {_currentDocument?.Sections.Count.ToString() ?? "none"}");
            if (_currentDocument is null || _currentDocument.Sections.Count == 0)
            {
                return;
            }

            await SaveToKnowledgeBaseAsync();
            ChplImportDiagnostics.Write("Knowledge Base import command completed.");
        }
        catch (Exception ex)
        {
            ChplImportDiagnostics.Write("Automatic ChPL import failed.", ex);
            StatusMessage = $"Nie udało się pobrać ChPL: {ex.Message}";
            MessageBox.Show(StatusMessage, "Pobieranie ChPL", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenRawTextWindow()
    {
        if (string.IsNullOrWhiteSpace(RawText))
        {
            StatusMessage = "Brak surowego tekstu do pokazania.";
            return;
        }

        var window = new RawChplTextWindow(RawText)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.ShowDialog();
    }

    [RelayCommand]
    private async Task SelectPdfAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Wybierz plik ChPL PDF",
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SelectedPdfPath = dialog.FileName;

        await ExtractAndParseAsync();
    }

    [RelayCommand]
    private async Task ExtractAndParseAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPdfPath))
        {
            StatusMessage = "Nie wybrano pliku PDF.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Odczytywanie PDF i parsowanie sekcji ChPL...";
            ChplImportDiagnostics.Write($"PDF extraction started: {SelectedPdfPath}");

            var filePath = SelectedPdfPath;

            _currentFileHash = await ComputeFileHashAsync(filePath);

            var extractedText = await Task.Run(
                () => _pdfTextExtractor.ExtractText(filePath));

            RawText = extractedText;
            ChplImportDiagnostics.Write($"PDF text extracted. Characters: {RawText.Length}");

            var parsedSections = await Task.Run(() =>
            {
                var sections = _sectionParser.ParseSections(extractedText);
                _tagger.TagSections(sections);
                return sections;
            });

            Sections.Clear();

            foreach (var section in parsedSections)
            {
                Sections.Add(section);
            }

            _currentDocument = new ChplDocument
            {
                RplProductId = SelectedProductContext?.RplProductId,
                SourceFile = Path.GetFileName(filePath),
                DocumentType = "ChPL",
                Language = "pl",
                ProductName = SelectedProductContext?.ProductName ?? ExtractProductNameFromFileName(filePath),
                ActiveSubstanceText = SelectedProductContext?.ActiveSubstanceText,
                ChplUrl = SelectedProductContext?.ChplUrl,
                ParsedAt = DateTime.UtcNow,
                Sections = parsedSections
            };
            ChplImportDiagnostics.Write($"Document model created. Sections: {_currentDocument.Sections.Count}");

            SelectedSection = Sections.FirstOrDefault();

            StatusMessage = Sections.Count == 0
                ? "Nie wykryto sekcji ChPL. Sprawdź, czy PDF zawiera zaznaczalny tekst."
                : $"Wykryto sekcje: {Sections.Count}. Liczba znaków: {RawText.Length:N0}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd przetwarzania PDF: {ex.Message}";

            MessageBox.Show(
                StatusMessage,
                "Błąd ChPL Navigator",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }


    [RelayCommand]
    private async Task SaveToKnowledgeBaseAsync()
    {
        if (_currentDocument is null)
        {
            StatusMessage = "Najpierw wybierz i sparsuj PDF.";
            return;
        }

        if (_currentDocument.Sections.Count == 0)
        {
            StatusMessage = "Nie można zapisać dokumentu bez wykrytych sekcji.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Zapisywanie ChPL do Knowledge Base...";
            ChplImportDiagnostics.Write($"Knowledge Base save started. File hash: {_currentFileHash ?? "none"}; Sections: {_currentDocument.Sections.Count}");

            var result = await _knowledgeBaseIngestionService.IngestChplDocumentAsync(
                _currentDocument,
                SelectedPdfPath,
                _currentFileHash);

            if (result.WasAlreadyImported)
            {
                ChplImportDiagnostics.Write($"Knowledge Base import skipped: document {result.ChplDocumentId} already exists.");
                StatusMessage =
                    $"Ten dokument jest już w Knowledge Base. ID dokumentu: {result.ChplDocumentId}.";
                return;
            }

            StatusMessage =
                $"Zapisano do Knowledge Base. Dokument ID: {result.ChplDocumentId}. " +
                $"Sekcje: {result.SectionsSaved}. Chunks: {result.ChunksSaved}. " +
                $"Status: {result.ReviewStatus}.";
            ChplImportDiagnostics.Write($"Knowledge Base save completed. Document: {result.ChplDocumentId}; Sections: {result.SectionsSaved}; Chunks: {result.ChunksSaved}; Status: {result.ReviewStatus}");
        }
        catch (Exception ex)
        {
            ChplImportDiagnostics.Write("Knowledge Base save failed.", ex);
            StatusMessage = $"Błąd zapisu do Knowledge Base: {ex.Message}";

            MessageBox.Show(
                StatusMessage,
                "Błąd Knowledge Base",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        if (_currentDocument is null)
        {
            StatusMessage = "Najpierw wybierz i sparsuj PDF.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Zapisz sekcje ChPL jako JSON",
            Filter = "JSON files (*.json)|*.json",
            FileName = BuildOutputFileName(".json")
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _jsonExporter.ExportAsync(_currentDocument, dialog.FileName);
            StatusMessage = $"Zapisano JSON: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            ChplImportDiagnostics.Write("PDF extraction or parsing failed.", ex);
            StatusMessage = $"Nie udało się zapisać JSON: {ex.Message}";
            MessageBox.Show(StatusMessage, "Błąd eksportu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (_currentDocument is null)
        {
            StatusMessage = "Najpierw wybierz i sparsuj PDF.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Zapisz sekcje ChPL jako CSV",
            Filter = "CSV files (*.csv)|*.csv",
            FileName = BuildOutputFileName(".csv")
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _csvExporter.ExportAsync(_currentDocument, dialog.FileName);
            StatusMessage = $"Zapisano CSV: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nie udało się zapisać CSV: {ex.Message}";
            MessageBox.Show(StatusMessage, "Błąd eksportu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Clear()
    {
        SelectedPdfPath = null;
        RawText = string.Empty;
        SelectedSection = null;
        StatusMessage = "Wyczyszczono dane ChPL.";

        Sections.Clear();

        _currentDocument = null;
        _currentFileHash = null;
        SelectedProductContext = null;
    }

    private string BuildOutputFileName(string extension)
    {
        var baseName = string.IsNullOrWhiteSpace(SelectedPdfPath)
            ? "chpl_export"
            : Path.GetFileNameWithoutExtension(SelectedPdfPath);

        return $"{baseName}_sections{extension}";
    }
    private static string? ExtractProductNameFromFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var cleaned = fileName
            .Replace("Charakterystyka-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Charakterystyka", "", StringComparison.OrdinalIgnoreCase)
            .Replace("ChPL", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", " ")
            .Replace("-", " ")
            .Trim();

        var parts = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !LooksLikeDateOrDocumentId(part))
            .ToList();

        if (parts.Count == 0)
        {
            return null;
        }

        return string.Join(" ", parts);
    }

    private static bool LooksLikeDateOrDocumentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.Any(char.IsDigit))
        {
            return true;
        }

        if (value.Equals("N", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return value.Length <= 1;
    }
    private static async Task<string> ComputeFileHashAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
