using System.Windows;
using System.Windows.Controls;
using StockFlow.UI.ViewModels;

namespace StockFlow.UI.Views;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProductsViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
