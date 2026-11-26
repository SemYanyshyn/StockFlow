using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic;
using StockFlow.Domain;

namespace StockFlow.UI.ViewModels;

public sealed partial class StockMovementEditorViewModel : ViewModelBase
{
    private readonly IStockMovementEditorService _service;
    private readonly Func<Task> _onSaved;

    public StockMovementEditorViewModel(StockMovementEditorData data, IStockMovementEditorService service,
        Action onCancelled, Func<Task> onSaved)
    {
        _service = service;
        _onSaved = onSaved;
        Number = data.Number;
        ResponsibleName = data.ResponsibleName;
        Products = data.Products;
        Warehouses = data.Warehouses;
        Suppliers = data.Suppliers;
        _selectedType = Types[0];
        CancelCommand = new RelayCommand(onCancelled, () => !IsSaving);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
    }

    public string Number { get; }
    public string ResponsibleName { get; }
    public IReadOnlyList<Product> Products { get; }
    public IReadOnlyList<Warehouse> Warehouses { get; }
    public IReadOnlyList<Supplier> Suppliers { get; }
    public IReadOnlyList<MovementTypeOption> Types { get; } = new[]
    {
        new MovementTypeOption(MovementType.Receipt, "Надходження"),
        new MovementTypeOption(MovementType.WriteOff, "Списання")
    };

    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public bool ShowsSupplier => SelectedType?.Value == MovementType.Receipt;
    public bool CanEdit => !IsSaving;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowsSupplier))]
    private MovementTypeOption? _selectedType;
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private Warehouse? _selectedWarehouse;
    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private DateTime? _date = DateTime.Today;
    [ObservableProperty] private string _quantityText = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand), nameof(SaveCommand))]
    private bool _isSaving;

    partial void OnSelectedTypeChanged(MovementTypeOption? value)
    {
        if (value?.Value != MovementType.Receipt) SelectedSupplier = null;
        ErrorMessage = null;
    }

    private async Task SaveAsync()
    {
        if (IsSaving) return;
        ErrorMessage = null;
        if (SelectedType?.Value is not { } type) { ErrorMessage = "Оберіть тип накладної."; return; }
        if (Date is not { } date || date == default) { ErrorMessage = "Потрібно вказати дату руху товару"; return; }
        if (SelectedProduct is not { } product) { ErrorMessage = "Оберіть товар."; return; }
        if (SelectedWarehouse is not { } warehouse) { ErrorMessage = "Оберіть склад."; return; }
        if (!int.TryParse(QuantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        {
            ErrorMessage = "Кількість має бути більшою за 0";
            return;
        }
        if (ShowsSupplier && SelectedSupplier is null)
        {
            ErrorMessage = "Для надходження потрібно обрати постачальника";
            return;
        }

        var request = new StockMovement
        {
            Number = Number,
            MovementDate = date.Date,
            Type = type,
            Quantity = quantity,
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            SupplierId = ShowsSupplier ? SelectedSupplier?.Id : null,
            Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()
        };
        IsSaving = true;
        try
        {
            var result = await Task.Run(() => _service.Save(request));
            if (!result.IsSuccess) { ErrorMessage = result.Error; return; }
            await _onSaved();
        }
        catch (Exception) { ErrorMessage = "Не вдалося зберегти накладну. Спробуйте ще раз."; }
        finally { IsSaving = false; }
    }
}
