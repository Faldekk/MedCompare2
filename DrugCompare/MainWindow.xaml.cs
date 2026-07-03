using System.Windows;
using DrugCompare.ViewModels;

namespace DrugCompare;

public partial class MainWindow : Window
{
    private bool _isNavigationCollapsed;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
        MainTabs.SelectedIndex = 0;
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.Tag is null)
        {
            return;
        }

        if (!int.TryParse(element.Tag.ToString(), out var tabIndex))
        {
            return;
        }

        MainTabs.SelectedIndex = tabIndex;
    }

    private void ToggleNavigation_Click(object sender, RoutedEventArgs e)
    {
        _isNavigationCollapsed = !_isNavigationCollapsed;

        NavigationColumn.Width = _isNavigationCollapsed
            ? new GridLength(64)
            : new GridLength(270);

        ExpandedNavigation.Visibility = _isNavigationCollapsed
            ? Visibility.Collapsed
            : Visibility.Visible;

        CollapsedNavigation.Visibility = _isNavigationCollapsed
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}