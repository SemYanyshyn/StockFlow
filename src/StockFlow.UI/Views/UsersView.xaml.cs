using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using StockFlow.UI.ViewModels;

namespace StockFlow.UI.Views;

public partial class UsersView : UserControl
{
    private UsersViewModel? _viewModel;
    private bool _loaded;
    public UsersView()
    {
        InitializeComponent();
        Unloaded += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.FormReset -= ClearPassword;
                _viewModel.PropertyChanged -= OnViewModelChanged;
            }
            Password.Clear();
            _loaded = false;
        };
    }
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UsersViewModel.CanManage)) UpdateActionsColumn();
    }
    private void UpdateActionsColumn() =>
        ActionsColumn.Visibility = _viewModel?.CanManage == true ? Visibility.Visible : Visibility.Collapsed;

    private void ClearPassword() => Password.Clear();
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded || DataContext is not UsersViewModel viewModel) return;
        _loaded = true;
        _viewModel = viewModel;
        _viewModel.FormReset += ClearPassword;
        _viewModel.PropertyChanged += OnViewModelChanged;
        UpdateActionsColumn();
        await viewModel.LoadAsync();
    }
    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _viewModel.IsBusy) return;
        var password = Password.Password;
        Password.Clear();
        await _viewModel.SaveCommand.ExecuteAsync(password);
    }
}
