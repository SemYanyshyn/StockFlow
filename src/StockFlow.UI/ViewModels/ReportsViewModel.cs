using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using Microsoft.Win32;
using StockFlow.BusinessLogic;

namespace StockFlow.UI.ViewModels;

public class ReportsViewModel : ViewModelBase
{
    private const string StockReportType = "Залишки";
    private const string MovementReportType = "Рух товару за період";

    private readonly StockReportingQueryService _queryService;

    public ObservableCollection<string> ReportTypes { get; } = new()
    {
        StockReportType,
        MovementReportType
    };

    public ObservableCollection<StockBalanceRowItem> StockRows { get; } = new();
    public ObservableCollection<MovementReportRowItem> MovementRows { get; } = new();

    private string _selectedReportType = StockReportType;
    public string SelectedReportType
    {
        get => _selectedReportType;
        set
        {
            if (_selectedReportType != value && !string.IsNullOrEmpty(value))
            {
                _selectedReportType = value;
                OnPropertyChanged(nameof(SelectedReportType));
                OnPropertyChanged(nameof(IsStockReport));
                OnPropertyChanged(nameof(IsMovementReport));
                OnPropertyChanged(nameof(IsPeriodVisible));
                GenerateReport();
            }
        }
    }

    public bool IsStockReport => SelectedReportType == StockReportType;
    public bool IsMovementReport => SelectedReportType == MovementReportType;
    public bool IsPeriodVisible => IsMovementReport;

    private DateTime? _dateFrom = DateTime.Today.AddMonths(-1);
    public DateTime? DateFrom
    {
        get => _dateFrom;
        set
        {
            if (_dateFrom != value)
            {
                _dateFrom = value;
                OnPropertyChanged(nameof(DateFrom));
            }
        }
    }

    private DateTime? _dateTo = DateTime.Today;
    public DateTime? DateTo
    {
        get => _dateTo;
        set
        {
            if (_dateTo != value)
            {
                _dateTo = value;
                OnPropertyChanged(nameof(DateTo));
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

    public ICommand GenerateReportCommand { get; }
    public ICommand ExportCsvCommand { get; }

    public ReportsViewModel(StockReportingQueryService queryService)
    {
        _queryService = queryService;

        GenerateReportCommand = new DelegateCommand(GenerateReport);
        ExportCsvCommand = new DelegateCommand(ExportCsv);

        GenerateReport();
    }

    private void GenerateReport()
    {
        try
        {
            ErrorMessage = string.Empty;

            if (IsStockReport)
            {
                StockRows.Clear();
                foreach (var row in _queryService.GetStockBalances())
                {
                    StockRows.Add(new StockBalanceRowItem(
                        ProductName: row.ProductName,
                        WarehouseName: row.WarehouseName,
                        Quantity: row.Quantity,
                        MinThreshold: row.MinThreshold,
                        UnitDisplay: StockBalancesViewModel.FormatUnit(row.Unit),
                        IsLow: row.IsLow));
                }
            }
            else
            {
                MovementRows.Clear();
                foreach (var row in _queryService.GetMovementSummary(DateFrom, DateTo))
                {
                    var unit = StockBalancesViewModel.FormatUnit(row.Unit);
                    MovementRows.Add(new MovementReportRowItem(
                        ProductName: row.ProductName,
                        WarehouseName: row.WarehouseName,
                        IncomingDisplay: $"+{row.IncomingQuantity} {unit}",
                        OutgoingDisplay: $"-{row.OutgoingQuantity} {unit}",
                        BalanceDisplay: $"{row.CurrentBalance} {unit}"));
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ExportCsv()
    {
        try
        {
            ErrorMessage = string.Empty;

            var dialog = new SaveFileDialog
            {
                Filter = "CSV файл (*.csv)|*.csv",
                FileName = IsStockReport
                    ? $"stock_report_{DateTime.Today:yyyy-MM-dd}.csv"
                    : $"movements_report_{DateTime.Today:yyyy-MM-dd}.csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            var sb = new StringBuilder();
            if (IsStockReport)
            {
                sb.AppendLine("Товар;Склад;Поточний залишок;Мін. норма;Статус");
                foreach (var r in StockRows)
                    sb.AppendLine($"{Escape(r.ProductName)};{Escape(r.WarehouseName)};{Escape(r.QuantityDisplay)};{Escape(r.MinThresholdDisplay)};{Escape(r.StatusText)}");
            }
            else
            {
                sb.AppendLine("Товар;Склад;Надходження;Списання;Залишок");
                foreach (var r in MovementRows)
                    sb.AppendLine($"{Escape(r.ProductName)};{Escape(r.WarehouseName)};{Escape(r.IncomingDisplay)};{Escape(r.OutgoingDisplay)};{Escape(r.BalanceDisplay)}");
            }

            File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private static string Escape(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private sealed class DelegateCommand : ICommand
    {
        private readonly Action _execute;
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public DelegateCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}

public sealed record MovementReportRowItem(
    string ProductName,
    string WarehouseName,
    string IncomingDisplay,
    string OutgoingDisplay,
    string BalanceDisplay);
