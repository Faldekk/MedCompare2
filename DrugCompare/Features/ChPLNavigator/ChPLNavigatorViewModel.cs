using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Services.Contracts.KnowledgeBase;
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

    private ChplDocument? _currentDocument;
    private string? _currentFileHash;

    public ChPLNavigatorViewModel(
        IKnowledgeBaseIngestionService knowledgeBaseIngestionService)
    {
        _knowledgeBaseIngestionService = knowledgeBaseIngestionService;
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

    public ObservableCollection<ChplSection> Sections { get; } = new();

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

            var filePath = SelectedPdfPath;

            _currentFileHash = await ComputeFileHashAsync(filePath);

            var extractedText = await Task.Run(
                () => _pdfTextExtractor.ExtractText(filePath));

            RawText = extractedText;

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
                SourceFile = Path.GetFileName(filePath),
                DocumentType = "ChPL",
                Language = "pl",
                ProductName = ExtractProductNameFromFileName(filePath),
                ParsedAt = DateTime.UtcNow,
                Sections = parsedSections
            };

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

            var result = await _knowledgeBaseIngestionService.IngestChplDocumentAsync(
                _currentDocument,
                SelectedPdfPath,
                _currentFileHash);

            if (result.WasAlreadyImported)
            {
                StatusMessage =
                    $"Ten dokument jest już w Knowledge Base. ID dokumentu: {result.ChplDocumentId}.";
                return;
            }

            StatusMessage =
                $"Zapisano do Knowledge Base. Dokument ID: {result.ChplDocumentId}. " +
                $"Sekcje: {result.SectionsSaved}. Chunks: {result.ChunksSaved}. " +
                $"Status: {result.ReviewStatus}.";
        }
        catch (Exception ex)
        {
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

        await _jsonExporter.ExportAsync(_currentDocument, dialog.FileName);

        StatusMessage = $"Zapisano JSON: {dialog.FileName}";
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

        await _csvExporter.ExportAsync(_currentDocument, dialog.FileName);

        StatusMessage = $"Zapisano CSV: {dialog.FileName}";
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