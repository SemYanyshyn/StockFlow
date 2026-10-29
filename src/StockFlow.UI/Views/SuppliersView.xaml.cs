using System.Windows;
using System.Windows.Controls;
using StockFlow.UI.ViewModels;

namespace StockFlow.UI.Views;

public partial class SuppliersView : UserControl
{
    private bool _loaded;

    public SuppliersView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded || DataContext is not SuppliersViewModel viewModel) return;
        _loaded = true;
        await viewModel.LoadAsync();
    }
}
