using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    private IReadOnlyList<Warehouse> _allWarehouses = Array.Empty<Warehouse>();

    public ObservableCollection<Warehouse> FilteredWarehouses { get; } = new();

    public string FormTitle => "Новий склад";

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _formName = string.Empty;
    [ObservableProperty] private string _formAddress = string.Empty;
    [ObservableProperty] private bool _isFormOpen;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;

    public bool CanManage => _currentUserContext.IsAdmin;

    public WarehousesViewModel(WarehouseService warehouseService, ICurrentUserContext currentUserContext)
    {
        _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
        _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            _allWarehouses = await Task.Run(() => _warehouseService.GetAll());
        }
        catch (Exception ex)
        {
            _allWarehouses = Array.Empty<Warehouse>();
            ErrorMessage = "Не вдалося завантажити склади. " + ex.Message;
        }
        finally
        {
            IsLoading = false;
            ApplyFilter();
        }
    }

    [RelayCommand]
    private void OpenCreateForm()
    {
        if (!CanManage) return;
        FormName = string.Empty;
        FormAddress = string.Empty;
        ErrorMessage = string.Empty;
        IsFormOpen = true;
    }

    [RelayCommand]
    private void CancelForm()
    {
        IsFormOpen = false;
        FormName = string.Empty;
        FormAddress = string.Empty;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SaveWarehouseAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            await Task.Run(() => _warehouseService.Add(FormName, FormAddress));
            CancelForm();
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

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        FilteredWarehouses.Clear();
        foreach (var warehouse in _allWarehouses.Where(w => w.MatchesSearch(query)))
            FilteredWarehouses.Add(warehouse);
    }
}
