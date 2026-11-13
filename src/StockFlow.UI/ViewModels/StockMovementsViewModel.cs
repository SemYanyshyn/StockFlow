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
    private bool _warehousesLoaded;

    public StockMovementsViewModel(StockMovementQueryService query, WarehouseService warehouses, ICurrentUserContext user)
    {
        _query = query;
        _warehouses = warehouses;
        IsEmployee = user.Role == UserRole.Employee;
        ApplyFiltersCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        ClearFiltersCommand = new AsyncRelayCommand(ClearAsync, () => !IsBusy);
        ViewCommand = new AsyncRelayCommand<StockMovementRow>(ViewAsync, row => !IsBusy && row is not null);
        BackCommand = new RelayCommand(() => Details = null, () => !IsBusy);
    }

    public bool IsEmployee { get; }
    public string? CreationUnavailableMessage => IsEmployee
        ? "Створення накладних тимчасово недоступне. Перегляд і фільтри працюють." : null;
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

    public IAsyncRelayCommand ApplyFiltersCommand { get; }
    public IAsyncRelayCommand ClearFiltersCommand { get; }
    public IAsyncRelayCommand<StockMovementRow> ViewCommand { get; }
    public IRelayCommand BackCommand { get; }

    partial void OnIsBusyChanged(bool value)
    {
        ApplyFiltersCommand.NotifyCanExecuteChanged();
        ClearFiltersCommand.NotifyCanExecuteChanged();
        ViewCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
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

        try
        {
            if (!_warehousesLoaded)
            {
                FilterError = null;
                try
                {
                    var warehouses = await Task.Run(_warehouses.GetAll);
                    WarehouseOptions = new[] { new MovementWarehouseOption(null, "Усі склади") }
                        .Concat(warehouses.Select(w => new MovementWarehouseOption(w.Id, w.Name))).ToArray();
                    _warehousesLoaded = true;
                }
                catch (Exception) { FilterError = "Не вдалося завантажити перелік складів. Натисніть «Застосувати», щоб повторити."; }
            }
        }
        finally { IsBusy = false; }
    }

    private async Task ClearAsync()
    {
        SearchNumber = string.Empty;
        FilterType = null;
        FilterWarehouseId = null;
        DateFrom = DateTo = null;
        await LoadAsync();
    }

    private async Task ViewAsync(StockMovementRow? row)
    {
        if (IsBusy || row is null) return;
        IsBusy = true;
        ListError = null;
        try { Details = new(await Task.Run(() => _query.GetById(row.Id))); }
        catch (ValidationException error) { ListError = error.Message; }
        catch (Exception) { ListError = "Не вдалося завантажити деталі накладної. Спробуйте ще раз."; }
        finally { IsBusy = false; }
    }
}
