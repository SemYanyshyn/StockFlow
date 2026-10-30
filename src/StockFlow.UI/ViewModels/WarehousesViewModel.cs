using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic.Users;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Warehouses;
using StockFlow.Domain;

namespace StockFlow.UI.ViewModels;

public sealed partial class WarehousesViewModel : ViewModelBase
{
    private readonly WarehouseService _warehouseService;
    private readonly ICurrentUserContext _currentUserContext;

    public ObservableCollection<Warehouse> Warehouses { get; } = new();

    [ObservableProperty] private string _newWarehouseName = string.Empty;
    [ObservableProperty] private string _newWarehouseAddress = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;

    public bool CanManage => _currentUserContext.IsAdmin;

    public WarehousesViewModel(WarehouseService warehouseService, ICurrentUserContext currentUserContext)
    {
        _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var warehouses = await Task.Run(() => _warehouseService.GetAll());
            App.Current.Dispatcher.Invoke(() =>
            {
                Warehouses.Clear();
                foreach (var w in warehouses)
                    Warehouses.Add(w);
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не вдалося завантажити склади. " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddWarehouseAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            await Task.Run(() => _warehouseService.Add(NewWarehouseName, NewWarehouseAddress));
            NewWarehouseName = string.Empty;
            NewWarehouseAddress = string.Empty;
            await LoadAsync();
        }
        catch (ValidationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не вдалося додати склад. " + ex.Message;
        }
    }
}