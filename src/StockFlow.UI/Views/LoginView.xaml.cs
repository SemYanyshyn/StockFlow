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
        Closed += (_, _) =>
        {
            _viewModel.LoginSucceeded -= OnSucceeded;
            Password.Clear();
            RevealedPassword.Clear();
        };
        Loaded += (_, _) => UsernameInput.Focus();
    }

    private void OnRevealPassword(object sender, RoutedEventArgs e)
    {
        RevealedPassword.Text = Password.Password;
        Password.Clear();
        Password.Visibility = Visibility.Collapsed;
        RevealedPassword.Visibility = Visibility.Visible;
        RevealedPassword.Focus();
        RevealedPassword.CaretIndex = RevealedPassword.Text.Length;
    }

    private void OnHidePassword(object sender, RoutedEventArgs e)
    {
        Password.Password = RevealedPassword.Text;
        RevealedPassword.Clear();
        RevealedPassword.Visibility = Visibility.Collapsed;
        Password.Visibility = Visibility.Visible;
        Password.Focus();
    }

    private void OnSucceeded() => DialogResult = true;
    private async void OnLogin(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsBusy) return;
        var password = RevealToggle.IsChecked == true ? RevealedPassword.Text : Password.Password;
        Password.Clear();
        RevealedPassword.Clear();
        RevealToggle.IsChecked = false;
        await _viewModel.LoginCommand.ExecuteAsync(password);
    }
}
