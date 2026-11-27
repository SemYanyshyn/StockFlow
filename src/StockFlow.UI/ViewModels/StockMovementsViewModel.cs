using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Suppliers;
using StockFlow.BusinessLogic.Users;
using StockFlow.BusinessLogic.Warehouses;
using StockFlow.Domain;

namespace StockFlow.UI.ViewModels;

public sealed partial class StockMovementsViewModel : ViewModelBase
{
    private readonly StockMovementQueryService _query;
    private readonly WarehouseService _warehouses;
    private readonly ProductService _products;
    private readonly SupplierService _suppliers;
    private readonly StockMovementService _registration;
    private bool _warehousesLoaded;
    private bool _reloadPending;
    private bool _suppressApply;
    private Task _loading = Task.CompletedTask;
    private CancellationTokenSource? _searchDelay;

    public StockMovementsViewModel(
        StockMovementQueryService query,
        WarehouseService warehouses,
        ProductService products,
        SupplierService suppliers,
        StockMovementService registration,
        ICurrentUserContext user)
    {
        _query = query;
        _warehouses = warehouses;
        _products = products;
        _suppliers = suppliers;
        _registration = registration;
        IsEmployee = user.Role == UserRole.Employee;
        CreateTypes = new[]
        {
            new MovementTypeOption(MovementType.Receipt, "Надходження"),
            new MovementTypeOption(MovementType.WriteOff, "Списання")
        };
        _selectedCreateType = CreateTypes[0];
        ClearFiltersCommand = new AsyncRelayCommand(ClearAsync, () => !IsBusy);
        ViewCommand = new AsyncRelayCommand<StockMovementRow>(ViewAsync, row => !IsBusy && row is not null);
        BackCommand = new RelayCommand(() => Details = null, () => !IsBusy);
        OpenCreateCommand = new AsyncRelayCommand(OpenCreateAsync, () => CanOpenCreate);
        SaveMovementCommand = new AsyncRelayCommand(SaveMovementAsync, () => CanSaveMovement);
        CancelCreateCommand = new RelayCommand(CancelCreate, () => !IsBusy && IsCreateFormOpen);
    }

    public bool IsEmployee { get; }
    public bool IsReceipt => SelectedCreateType.Value == MovementType.Receipt;
    public bool IsListOpen => Details is null;
    public string Title => Details is null ? "Накладні" : "Перегляд накладної";
    public IReadOnlyList<MovementTypeOption> FilterTypes { get; } = new[]
    {
        new MovementTypeOption(null, "Усі типи"), new(MovementType.Receipt, "Надходження"), new(MovementType.WriteOff, "Списання")
    };
    public MovementTypeOption SelectedFilterType
    {
        get => FilterTypes.FirstOrDefault(option => option.Value == FilterType) ?? FilterTypes[0];
        set => FilterType = value?.Value;
    }
    public MovementWarehouseOption SelectedFilterWarehouse
    {
        get => WarehouseOptions.FirstOrDefault(option => option.Id == FilterWarehouseId) ?? WarehouseOptions[0];
        set => FilterWarehouseId = value?.Id;
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SelectedFilterWarehouse))]
    private IReadOnlyList<MovementWarehouseOption> _warehouseOptions = new[] { new MovementWarehouseOption(null, "Усі склади") };
    [ObservableProperty] private IReadOnlyList<StockMovementRow> _movements = Array.Empty<StockMovementRow>();
    [ObservableProperty] private string _searchNumber = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SelectedFilterType))] private MovementType? _filterType;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SelectedFilterWarehouse))] private int? _filterWarehouseId;
    [ObservableProperty] private DateTime? _dateFrom;
    [ObservableProperty] private DateTime? _dateTo;
    [ObservableProperty] private string? _emptyMessage;
    [ObservableProperty] private string? _listError;
    [ObservableProperty] private string? _filterError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsListOpen), nameof(Title))] private StockMovementRow? _details;
    [ObservableProperty] private bool _isCreateFormOpen;
    [ObservableProperty] private string _creationNumber = string.Empty;
    [ObservableProperty] private string _creationQuantity = string.Empty;
    [ObservableProperty] private DateTime? _creationDate = DateTime.Today;
    [ObservableProperty] private string _creationNote = string.Empty;
    [ObservableProperty] private string? _creationError;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsReceipt))]
    private MovementTypeOption _selectedCreateType = new(MovementType.Receipt, "Надходження");
    [ObservableProperty] private IReadOnlyList<Warehouse> _createWarehouses = Array.Empty<Warehouse>();
    [ObservableProperty] private Warehouse? _selectedCreateWarehouse;
    [ObservableProperty] private IReadOnlyList<Product> _createProducts = Array.Empty<Product>();
    [ObservableProperty] private Product? _selectedCreateProduct;
    [ObservableProperty] private IReadOnlyList<Supplier> _createSuppliers = Array.Empty<Supplier>();
    [ObservableProperty] private Supplier? _selectedCreateSupplier;

    public IAsyncRelayCommand ClearFiltersCommand { get; }
    public IAsyncRelayCommand<StockMovementRow> ViewCommand { get; }
    public IRelayCommand BackCommand { get; }
    public IAsyncRelayCommand OpenCreateCommand { get; }
    public IAsyncRelayCommand SaveMovementCommand { get; }
    public IRelayCommand CancelCreateCommand { get; }

    public IReadOnlyList<MovementTypeOption> CreateTypes { get; }

    internal TimeSpan SearchDelay { get; set; } = TimeSpan.FromMilliseconds(300);
    internal Task PendingApply { get; private set; } = Task.CompletedTask;

    partial void OnIsBusyChanged(bool value)
    {
        ClearFiltersCommand.NotifyCanExecuteChanged();
        ViewCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        OpenCreateCommand.NotifyCanExecuteChanged();
        SaveMovementCommand.NotifyCanExecuteChanged();
        CancelCreateCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCreateFormOpenChanged(bool value)
    {
        OpenCreateCommand.NotifyCanExecuteChanged();
        SaveMovementCommand.NotifyCanExecuteChanged();
        CancelCreateCommand.NotifyCanExecuteChanged();
    }
    partial void OnSelectedCreateTypeChanged(MovementTypeOption value)
    {
        CreationError = null;
        if (value.Value != MovementType.Receipt)
            SelectedCreateSupplier = null;
    }

    partial void OnSearchNumberChanged(string value) => ApplyAfterTyping();
    partial void OnFilterTypeChanged(MovementType? value) => ApplyNow();
    partial void OnFilterWarehouseIdChanged(int? value) => ApplyNow();
    partial void OnDateFromChanged(DateTime? value) => ApplyNow();
    partial void OnDateToChanged(DateTime? value) => ApplyNow();

    public Task LoadAsync()
    {
        if (IsBusy)
        {
            _reloadPending = true;
            return _loading;
        }
        _loading = RunLoadAsync();
        return _loading;
    }

    private async Task RunLoadAsync()
    {
        IsBusy = true;
        try
        {
            do
            {
                _reloadPending = false;
                await LoadMovementsAsync();
                await LoadWarehousesAsync();
            }
            while (_reloadPending);
        }
        finally { IsBusy = false; }
    }

    private async Task LoadMovementsAsync()
    {
        ListError = null;
        EmptyMessage = null;
        var filter = new StockMovementFilter(SearchNumber, FilterType, FilterWarehouseId, DateFrom, DateTo);
        try
        {
            var result = await Task.Run(() => _query.Search(filter));
            Movements = result.Items.Select(item => new StockMovementRow(item)).ToArray();
            EmptyMessage = Movements.Count != 0 ? null : result.HasAny
                ? "За обраними фільтрами накладних не знайдено." : "Накладних ще немає.";
        }
        catch (ValidationException error) { ListError = error.Message; }
        catch (Exception)
        {
            Movements = Array.Empty<StockMovementRow>();
            ListError = "Не вдалося завантажити накладні. Перевірте з’єднання з базою даних і спробуйте ще раз.";
        }
    }

    private async Task LoadWarehousesAsync()
    {
        if (_warehousesLoaded) return;
        FilterError = null;
        try
        {
            var warehouses = await Task.Run(_warehouses.GetAll);
            WarehouseOptions = new[] { new MovementWarehouseOption(null, "Усі склади") }
                .Concat(warehouses.Select(w => new MovementWarehouseOption(w.Id, w.Name))).ToArray();
            _warehousesLoaded = true;
        }
        catch (Exception) { FilterError = "Не вдалося завантажити перелік складів. Натисніть «Очистити», щоб повторити."; }
    }

    private void ApplyNow()
    {
        if (_suppressApply) return;
        CancelSearchDelay();
        PendingApply = LoadAsync();
    }

    private void ApplyAfterTyping()
    {
        if (_suppressApply) return;
        CancelSearchDelay();
        _searchDelay = new CancellationTokenSource();
        PendingApply = LoadAfterDelayAsync(_searchDelay.Token);
    }

    private async Task LoadAfterDelayAsync(CancellationToken token)
    {
        try { await Task.Delay(SearchDelay, token); }
        catch (OperationCanceledException) { return; }
        await LoadAsync();
    }

    private void CancelSearchDelay()
    {
        _searchDelay?.Cancel();
        _searchDelay?.Dispose();
        _searchDelay = null;
    }

    private async Task ClearAsync()
    {
        CancelSearchDelay();
        _suppressApply = true;
        try
        {
            SearchNumber = string.Empty;
            FilterType = null;
            FilterWarehouseId = null;
            DateFrom = DateTo = null;
        }
        finally { _suppressApply = false; }
        await LoadAsync();
    }

    private async Task ViewAsync(StockMovementRow? row)
    {
        if (IsBusy || row is null) return;
        IsBusy = true;
        ListError = null;
        string? detailError = null;
        try { Details = new(await Task.Run(() => _query.GetById(row.Id))); }
        catch (ValidationException error) { detailError = error.Message; }
        catch (Exception) { detailError = "Не вдалося завантажити деталі накладної. Спробуйте ще раз."; }
        finally { IsBusy = false; }
        if (_reloadPending) await LoadAsync();
        ListError ??= detailError;
    }

    private bool CanOpenCreate => IsEmployee && !IsBusy && !IsCreateFormOpen;
    private bool CanSaveMovement => IsEmployee && IsCreateFormOpen && !IsBusy;

    private async Task OpenCreateAsync()
    {
        CreationError = null;
        IsCreateFormOpen = true;
        IsBusy = true;
        try
        {
            var (warehouses, products, suppliers) = await Task.Run(() =>
                (_warehouses.GetAll(), _products.GetAll(), _suppliers.GetAll()));
            CreateWarehouses = warehouses;
            CreateProducts = products;
            CreateSuppliers = suppliers;
            SelectedCreateWarehouse = warehouses.FirstOrDefault();
            SelectedCreateProduct = products.FirstOrDefault();
            SelectedCreateSupplier = suppliers.FirstOrDefault();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            CreationNumber = $"НК-{DateTime.Now:yyMMddHHmmss}-{suffix}";
            CreationQuantity = string.Empty;
            CreationDate = DateTime.Today;
            CreationNote = string.Empty;

            if (warehouses.Count == 0 || products.Count == 0 || IsReceipt && suppliers.Count == 0)
                CreationError = IsReceipt && suppliers.Count == 0
                    ? "Для надходження потрібно додати постачальника."
                    : "Для створення накладної потрібні товари та склади.";
        }
        catch (Exception)
        {
            CreationError = "Не вдалося завантажити дані для накладної. Перевірте з'єднання з базою даних.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveMovementAsync()
    {
        CreationError = null;
        if (string.IsNullOrWhiteSpace(CreationNumber))
        {
            CreationError = "Введіть номер накладної.";
            return;
        }
        if (SelectedCreateType.Value is not { } movementType)
        {
            CreationError = "Оберіть тип накладної.";
            return;
        }
        if (SelectedCreateWarehouse is null)
        {
            CreationError = "Оберіть склад.";
            return;
        }
        if (SelectedCreateProduct is null)
        {
            CreationError = "Оберіть товар.";
            return;
        }
        if (!int.TryParse(CreationQuantity, out var quantity) || quantity <= 0)
        {
            CreationError = "Кількість має бути додатним цілим числом.";
            return;
        }
        if (CreationDate is null)
        {
            CreationError = "Оберіть дату накладної.";
            return;
        }
        if (movementType == MovementType.Receipt && SelectedCreateSupplier is null)
        {
            CreationError = "Для надходження потрібно обрати постачальника.";
            return;
        }

        IsBusy = true;
        var saved = false;
        try
        {
            var movement = new StockMovement
            {
                Number = CreationNumber.Trim(),
                Type = movementType,
                ProductId = SelectedCreateProduct.Id,
                WarehouseId = SelectedCreateWarehouse.Id,
                SupplierId = movementType == MovementType.Receipt ? SelectedCreateSupplier?.Id : null,
                Quantity = quantity,
                MovementDate = CreationDate.Value,
                Note = string.IsNullOrWhiteSpace(CreationNote) ? null : CreationNote.Trim()
            };
            var result = await Task.Run(() => _registration.Register(movement));
            if (!result.IsSuccess)
            {
                CreationError = result.Error ?? "Не вдалося створити накладну.";
                return;
            }

            saved = true;
            ResetCreationForm();
        }
        catch (Exception)
        {
            CreationError = "Не вдалося створити накладну. Перевірте з'єднання з базою даних.";
        }
        finally
        {
            IsBusy = false;
        }

        if (saved)
            await LoadAsync();
    }

    private void CancelCreate()
    {
        if (!IsBusy)
            ResetCreationForm();
    }

    private void ResetCreationForm()
    {
        IsCreateFormOpen = false;
        CreationNumber = string.Empty;
        CreationQuantity = string.Empty;
        CreationDate = DateTime.Today;
        CreationNote = string.Empty;
        CreationError = null;
        SelectedCreateSupplier = null;
    }
}
