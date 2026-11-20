using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using StockFlow.BusinessLogic.Users;
using StockFlow.BusinessLogic.Warehouses;
using StockFlow.Domain;

namespace StockFlow.UI.ViewModels;

public class WarehousesViewModel : ViewModelBase
{
    private readonly WarehouseService _warehouseService;
    private readonly ICurrentUserContext _userContext;

    public ObservableCollection<Warehouse> Warehouses { get; } = new();
    public ObservableCollection<Warehouse> FilteredWarehouses { get; } = new();
    public ObservableCollection<string> Locations { get; } = new();

    public bool CanManage => _userContext.IsAdmin;

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value ?? "";
                OnPropertyChanged(nameof(SearchText));
                ApplyFilter();
            }
        }
    }

    private string _selectedLocation = "Усі";
    public string SelectedLocation
    {
        get => _selectedLocation;
        set
        {
            if (_selectedLocation != value)
            {
                _selectedLocation = value ?? "Усі";
                OnPropertyChanged(nameof(SelectedLocation));
                ApplyFilter();
            }
        }
    }

    private bool _isFormOpen;
    public bool IsFormOpen
    {
        get => _isFormOpen;
        set
        {
            _isFormOpen = value;
            OnPropertyChanged(nameof(IsFormOpen));
        }
    }

    private string _formTitle = "";
    public string FormTitle
    {
        get => _formTitle;
        set
        {
            _formTitle = value;
            OnPropertyChanged(nameof(FormTitle));
        }
    }

    private string _formName = "";
    public string FormName
    {
        get => _formName;
        set
        {
            _formName = value;
            OnPropertyChanged(nameof(FormName));
        }
    }

    private string _formAddress = "";
    public string FormAddress
    {
        get => _formAddress;
        set
        {
            _formAddress = value;
            OnPropertyChanged(nameof(FormAddress));
        }
    }

    private string _errorMessage = "";
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            OnPropertyChanged(nameof(ErrorMessage));
        }
    }

    private Warehouse? _editingWarehouse;

    public ICommand OpenCreateFormCommand { get; }
    public ICommand OpenEditFormCommand { get; }
    public ICommand CancelFormCommand { get; }
    public ICommand SaveWarehouseCommand { get; }
    public ICommand DeleteWarehouseCommand { get; }

    public WarehousesViewModel(
        WarehouseService warehouseService,
        ICurrentUserContext userContext)
    {
        _warehouseService = warehouseService;
        _userContext = userContext;

        OpenCreateFormCommand = new DelegateCommand(OpenCreateForm, () => CanManage);
        OpenEditFormCommand = new DelegateCommand<Warehouse>(OpenEditForm, w => CanManage && w != null);
        CancelFormCommand = new DelegateCommand(CancelForm);
        SaveWarehouseCommand = new DelegateCommand(SaveWarehouse, () => CanManage);
        DeleteWarehouseCommand = new DelegateCommand<Warehouse>(DeleteWarehouse, w => CanManage && w != null);

        LoadWarehouses();
    }

    private void LoadWarehouses()
    {
        try
        {
            ErrorMessage = "";
            Warehouses.Clear();
            foreach (var w in _warehouseService.GetAll())
                Warehouses.Add(w);

            Locations.Clear();
            Locations.Add("Усі");
            foreach (var city in Warehouses
                         .Select(w => GetCity(w.Address))
                         .Where(c => !string.IsNullOrEmpty(c))
                         .Distinct()
                         .OrderBy(x => x))
            {
                Locations.Add(city);
            }

            if (!Locations.Contains(SelectedLocation))
                SelectedLocation = "Усі";

            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ApplyFilter()
    {
        var filtered = Warehouses.Where(w =>
            (string.IsNullOrWhiteSpace(SearchText) ||
             w.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
             (w.Address?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true)) &&
            (SelectedLocation == "Усі" || GetCity(w.Address) == SelectedLocation)
        ).ToList();

        FilteredWarehouses.Clear();
        foreach (var w in filtered)
            FilteredWarehouses.Add(w);
    }

    private static string GetCity(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "";
        var parts = address.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0].Trim() : address.Trim();
    }

    private void OpenCreateForm()
    {
        _editingWarehouse = null;
        ErrorMessage = "";
        FormTitle = "Додати склад";
        FormName = "";
        FormAddress = "";
        IsFormOpen = true;
    }

    private void OpenEditForm(Warehouse? warehouse)
    {
        if (warehouse == null) return;
        _editingWarehouse = warehouse;
        ErrorMessage = "";
        FormTitle = "Редагувати склад";
        FormName = warehouse.Name;
        FormAddress = warehouse.Address ?? "";
        IsFormOpen = true;
    }

    private void CancelForm()
    {
        IsFormOpen = false;
        _editingWarehouse = null;
        ErrorMessage = "";
        FormName = "";
        FormAddress = "";
    }

    private void SaveWarehouse()
    {
        ErrorMessage = "";

        if (string.IsNullOrWhiteSpace(FormName))
        {
            ErrorMessage = "Назва складу обов'язкова.";
            return;
        }

        try
        {
            if (_editingWarehouse == null)
            {
                _warehouseService.Add(FormName, FormAddress);
            }
            else
            {
                _editingWarehouse.Name = FormName.Trim();
                _editingWarehouse.Address = string.IsNullOrWhiteSpace(FormAddress) ? null : FormAddress.Trim();
                _warehouseService.Update(_editingWarehouse);
            }

            LoadWarehouses();
            CancelForm();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void DeleteWarehouse(Warehouse? warehouse)
    {
        if (warehouse == null || !CanManage) return;

        var result = MessageBox.Show(
            $"Видалити склад «{warehouse.Name}»?",
            "Підтвердження видалення",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            ErrorMessage = "";
            _warehouseService.Delete(warehouse.Id);
            LoadWarehouses();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private sealed class DelegateCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public DelegateCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }

    private sealed class DelegateCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public DelegateCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => _execute((T?)parameter);
    }
}
