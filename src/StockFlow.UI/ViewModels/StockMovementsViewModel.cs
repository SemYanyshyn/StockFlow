using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.BusinessLogic.Warehouses;
using StockFlow.Domain;

namespace StockFlow.UI.ViewModels;

public sealed partial class StockMovementsViewModel : ViewModelBase
{
    private readonly StockMovementQueryService _query;
    private readonly WarehouseService _warehouses;
    private readonly IStockMovementEditorService _editorService;
    private bool _warehousesLoaded;
    private bool _reloadPending;
    private bool _suppressApply;
    private Task _loading = Task.CompletedTask;
    private CancellationTokenSource? _searchDelay;

    public StockMovementsViewModel(StockMovementQueryService query, WarehouseService warehouses,
        ICurrentUserContext user, IStockMovementEditorService editorService)
    {
        _query = query;
        _warehouses = warehouses;
        _editorService = editorService;
        IsEmployee = user.Role == UserRole.Employee;
        ClearFiltersCommand = new AsyncRelayCommand(ClearAsync, () => !IsBusy);
        ViewCommand = new AsyncRelayCommand<StockMovementRow>(ViewAsync, row => !IsBusy && row is not null);
        BackCommand = new RelayCommand(() => Details = null, () => !IsBusy);
        CreateCommand = new AsyncRelayCommand(OpenEditorAsync, () => CanCreateMovement && !IsBusy);
    }

    public bool IsEmployee { get; }
    public bool CanCreateMovement => IsEmployee && IsListOpen;
    public bool IsListOpen => Details is null && Editor is null;
    public bool HasDetails => Details is not null;
    public string Title => Editor is not null ? "Створення накладної" : HasDetails ? "Перегляд накладної" : "Накладні";
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
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsListOpen), nameof(HasDetails), nameof(Title), nameof(CanCreateMovement))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private StockMovementRow? _details;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsListOpen), nameof(Title), nameof(CanCreateMovement))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private StockMovementEditorViewModel? _editor;

    public IAsyncRelayCommand ClearFiltersCommand { get; }
    public IAsyncRelayCommand<StockMovementRow> ViewCommand { get; }
    public IRelayCommand BackCommand { get; }
    public IAsyncRelayCommand CreateCommand { get; }

    internal TimeSpan SearchDelay { get; set; } = TimeSpan.FromMilliseconds(300);
    internal Task PendingApply { get; private set; } = Task.CompletedTask;

    partial void OnIsBusyChanged(bool value)
    {
        ClearFiltersCommand.NotifyCanExecuteChanged();
        ViewCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        CreateCommand.NotifyCanExecuteChanged();
    }

    private async Task OpenEditorAsync()
    {
        if (!CanCreateMovement || IsBusy) return;
        IsBusy = true;
        ListError = null;
        string? editorError = null;
        try
        {
            var data = await Task.Run(_editorService.Load);
            Editor = new(data, _editorService, () => Editor = null, RefreshAfterSaveAsync);
        }
        catch (ValidationException error) { editorError = error.Message; }
        catch (Exception) { editorError = "Не вдалося відкрити форму накладної. Перевірте з’єднання і спробуйте ще раз."; }
        finally { IsBusy = false; }
        if (_reloadPending) await LoadAsync();
        ListError ??= editorError;
    }

    private async Task RefreshAfterSaveAsync()
    {
        Editor = null;
        await LoadAsync();
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
        await ExecuteSafelyAsync(
            async () => Details = new(await Task.Run(() => _query.GetById(row.Id))),
            error => ListError = error is ValidationException validationError
                ? validationError.Message
                : "Не вдалося завантажити деталі накладної. Спробуйте ще раз.",
            onStarting: () =>
            {
                IsBusy = true;
                ListError = null;
            },
            onFinally: () => IsBusy = false);
        if (_reloadPending) await LoadAsync();
    }
}
