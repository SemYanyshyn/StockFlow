using System.Windows;
using System.Windows.Controls;

namespace StockFlow.UI.Products;

public partial class CategoriesView : UserControl
{
    public CategoriesView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CategoriesViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
