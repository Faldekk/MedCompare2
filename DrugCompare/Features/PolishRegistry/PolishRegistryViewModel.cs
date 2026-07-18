using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugCompare.Application.Models;
using DrugCompare.Application.Services.Contracts;
using DrugCompare.Features.ChPLNavigator;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace DrugCompare.Features.PolishRegistry;

public sealed partial class PolishDrugRegistryViewModel : ObservableObject
{
    private readonly IPolishDrugRegistryService _polishDrugRegistryService;
    private readonly ChPLNavigatorViewModel _chplNavigator;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private PolishDrugRegistryItem? selectedResult;

    [ObservableProperty]
    private string statusMessage = "Gotowe.";

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<PolishDrugRegistryItem> Results { get; } = new();
    
    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(
                "Brak dostępnego linku.",
                "Dokument niedostępny",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Nie udało się otworzyć linku: {ex.Message}",
                "Błąd otwierania dokumentu",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public PolishDrugRegistryViewModel(
        IPolishDrugRegistryService polishDrugRegistryService,
        ChPLNavigatorViewModel chplNavigator)
    {
        _polishDrugRegistryService = polishDrugRegistryService;
        _chplNavigator = chplNavigator;
    }
    [RelayCommand]
    private void OpenChpl()
    {
        OpenUrl(SelectedResult?.ChplUrl);
    }

    [RelayCommand]
    private void OpenLeaflet()
    {
        OpenUrl(SelectedResult?.LeafletUrl);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await SearchAsync(SearchText);
    }

    [RelayCommand]
    private void SendToChplNavigator()
    {
        if (SelectedResult is null)
        {
            StatusMessage = "Najpierw wybierz produkt z wyników.";
            return;
        }

        _chplNavigator.SetProductContext(SelectedResult);
        StatusMessage = "Produkt przekazano do ChPL Navigator. Otwórz moduł ChPL Navigator i wybierz plik PDF.";
    }

    public async Task SearchAsync(string query)
    {
        try
        {
            IsBusy = true;
            SearchText = query;
            StatusMessage = "Wyszukiwanie w Polskim Rejestrze Produktów Leczniczych...";

            Results.Clear();
            SelectedResult = null;

            var items = await _polishDrugRegistryService.SearchAsync(query, limit: 100);

            foreach (var item in items)
            {
                Results.Add(item);
            }

            SelectedResult = Results.FirstOrDefault();

            StatusMessage = $"Znaleziono {Results.Count} produktów.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Błąd wyszukiwania w rejestrze: {ex.Message}";
            MessageBox.Show(
                StatusMessage,
                "Błąd Polish Registry",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        SearchText = string.Empty;
        Results.Clear();
        SelectedResult = null;
        StatusMessage = "Wyczyszczono.";
    }
}
