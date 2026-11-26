using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;

namespace StockFlow.UI.ViewModels;

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    public event Action? LoginSucceeded;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;
    public LoginViewModel(AuthService auth) => _auth = auth;

    [RelayCommand]
    private async Task LoginAsync(string? password)
    {
        if (IsBusy) return;
        await ExecuteSafelyAsync(async () =>
        {
            var username = Username;
            await Task.Run(() => _auth.Login(username, password));
        },
        error => ErrorMessage = error is ValidationException validationError
            ? validationError.Message
            : "Не вдалося увійти. Перевірте налаштування та підключення до бази даних і спробуйте ще раз.",
        onStarting: () =>
        {
            IsBusy = true;
            ErrorMessage = null;
        },
        onFinally: () => IsBusy = false);
        if (ErrorMessage is null) LoginSucceeded?.Invoke();
    }
}
