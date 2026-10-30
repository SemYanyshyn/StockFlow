using System.Windows;

namespace StockFlow.UI.Services;

public sealed class ConfirmationService : IConfirmationService
{
    public bool Confirm(string message) =>
        MessageBox.Show(message, "StockFlow", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
        == MessageBoxResult.Yes;
}
