using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Warehouses;
using StockFlow.Domain;

namespace StockFlow.UI.ViewModels;

public class StockBalancesViewModel : ViewModelBase
{
    private readonly StockService _stockService;
    private readonly WarehouseService _warehouseService;
    private readonly List<StockBalanceRowItem> _allRows = new();

    public ObservableCollection<StockBalanceRowItem> FilteredRows { get; } = new();
    public ObservableCollection<Warehouse> Warehouses { get; } = new();
    public bool IsEmpty => FilteredRows.Count == 0;

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            var normalized = value ?? string.Empty;
            if (_searchText != normalized)
            {
                _searchText = normalized;
                OnPropertyChanged(nameof(SearchText));
                ApplyFilter();
            }
        }
    }

    private Warehouse? _selectedWarehouse;
    public Warehouse? SelectedWarehouse
    {
        get => _selectedWarehouse;
        set
        {
            if (_selectedWarehouse != value)
            {
                _selectedWarehouse = value;
                OnPropertyChanged(nameof(SelectedWarehouse));
                ApplyFilter();
            }
        }
    }

    private bool _onlyCritical;
    public bool OnlyCritical
    {
        get => _onlyCritical;
        set
        {
            if (_onlyCritical != value)
            {
                _onlyCritical = value;
                OnPropertyChanged(nameof(OnlyCritical));
                ApplyFilter();
            }
        }
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }
    }

    public StockBalancesViewModel(
        StockService stockService,
        WarehouseService warehouseService)
    {
        _stockService = stockService;
        _warehouseService = warehouseService;

        LoadData();
    }

    private void LoadData()
    {
        try
        {
            ErrorMessage = string.Empty;

            Warehouses.Clear();
            var allOption = new Warehouse { Id = 0, Name = "Усі склади" };
            Warehouses.Add(allOption);
            foreach (var warehouse in _warehouseService.GetAll())
            {
                Warehouses.Add(warehouse);
            }

            _selectedWarehouse = allOption;
            OnPropertyChanged(nameof(SelectedWarehouse));

            _allRows.Clear();
            foreach (var row in _stockService.GetRows())
            {
                var unitDisplay = FormatUnit(row.Unit);
                _allRows.Add(new StockBalanceRowItem(
                    ProductName: row.ProductName,
                    WarehouseName: row.WarehouseName,
                    Quantity: row.Quantity,
                    MinThreshold: row.MinThreshold,
                    UnitDisplay: unitDisplay,
                    IsLow: row.IsLow));
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            FilteredRows.Clear();
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var filterByWarehouse = SelectedWarehouse is { Id: > 0 };
        var targetWarehouse = SelectedWarehouse?.Name;

        var filtered = _allRows.Where(item =>
            (string.IsNullOrEmpty(query) ||
             item.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
            (!filterByWarehouse ||
             string.Equals(item.WarehouseName, targetWarehouse, StringComparison.OrdinalIgnoreCase)) &&
            (!OnlyCritical || item.IsLow)
        ).ToList();

        FilteredRows.Clear();
        foreach (var item in filtered)
        {
            FilteredRows.Add(item);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    internal static string FormatUnit(string? unit) => unit?.ToLowerInvariant() switch
    {
        "piece" => "шт.",
        "kg" => "кг",
        "liter" => "л",
        _ => unit ?? "шт."
    };
}

public sealed record StockBalanceRowItem(
    string ProductName,
    string WarehouseName,
    int Quantity,
    int MinThreshold,
    string UnitDisplay,
    bool IsLow)
{
    public string QuantityDisplay => IsLow
        ? $"{Quantity} {UnitDisplay} \u26A0"
        : $"{Quantity} {UnitDisplay}";

    public string MinThresholdDisplay => $"{MinThreshold} {UnitDisplay}";

    public string StatusText => IsLow ? "\u26A0 Критично" : "\u2022 В нормі";
}
