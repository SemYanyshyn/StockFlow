using System.Windows;
using System.Windows.Controls;
using StockFlow.UI.ViewModels;

namespace StockFlow.UI.Views;

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
            ActionsColumn.Visibility = viewModel.CanManage ? Visibility.Visible : Visibility.Collapsed;
            viewModel.LoadCommand.Execute(null);
        }
    }
}
