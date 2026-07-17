using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using DrugCompare.ViewModels;

namespace DrugCompare.Features.PolishRegistry;

public partial class PolishRegistryView : UserControl
{
    public PolishRegistryView()
    {
        InitializeComponent();
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await SearchAsync();
    }

    private async void RegistrySearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        var viewModel = DataContext as PolishDrugRegistryViewModel
            ?? (Window.GetWindow(this)?.DataContext as MainViewModel)?.PolishDrugRegistry;

        if (viewModel is null)
        {
            MessageBox.Show(
                "Nie udało się zainicjować wyszukiwarki Polish Registry.",
                "Błąd konfiguracji widoku",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        await viewModel.SearchAsync(RegistrySearchTextBox.Text);
    }
}
