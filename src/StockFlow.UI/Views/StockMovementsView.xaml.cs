using System.Windows;
using System.Windows.Controls;
using StockFlow.UI.ViewModels;

namespace StockFlow.UI.Views;

public partial class StockMovementsView : UserControl
{
    private StockMovementsViewModel? _loadedViewModel;
    public StockMovementsView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await LoadViewModelAsync();

    private async void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => await LoadViewModelAsync();

    private async Task LoadViewModelAsync()
    {
        // WPF may reuse this DataTemplate when the menu creates another VM of the same type.
        if (!IsLoaded || DataContext is not StockMovementsViewModel viewModel || ReferenceEquals(_loadedViewModel, viewModel)) return;
        _loadedViewModel = viewModel;
        await viewModel.LoadAsync();
    }
}
