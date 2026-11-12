using System.Windows;
using StockFlow.UI.ViewModels;

namespace StockFlow.UI.Views;

public partial class LoginView : Window
{
    private readonly LoginViewModel _viewModel;
    public LoginView(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _viewModel.LoginSucceeded += OnSucceeded;
        Closed += (_, _) => _viewModel.LoginSucceeded -= OnSucceeded;
    }

    private void OnSucceeded() => DialogResult = true;
    private async void OnLogin(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy) return;
        var password = Password.Password;
        Password.Clear();
        await _viewModel.LoginCommand.ExecuteAsync(password);
    }
}
