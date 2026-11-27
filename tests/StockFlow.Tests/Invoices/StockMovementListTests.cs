using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Suppliers;
using StockFlow.BusinessLogic.Users;
using StockFlow.BusinessLogic.Warehouses;
using StockFlow.DataAccess;
using StockFlow.DataAccess.Products;
using StockFlow.DataAccess.Suppliers;
using StockFlow.DataAccess.Warehouses;
using StockFlow.Domain;
using StockFlow.UI.ViewModels;
using Xunit;

namespace StockFlow.Tests.Invoices;

public sealed class StockMovementListTests
{
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Employee)]
    public async Task BothRolesCanLoadAndViewReadOnlyDetails(UserRole role)
    {
        var f = new Fixture(role); var vm = f.ViewModel();
        await vm.LoadAsync();
        var row = Assert.Single(vm.Movements);
        Assert.Equal("5 кг", row.QuantityText);
        await vm.ViewCommand.ExecuteAsync(row);
        Assert.Equal("Примітка", vm.Details!.Data.Note);
        Assert.Equal("Постачальник", vm.Details.Data.SupplierName);
        Assert.False(vm.IsListOpen);
        vm.BackCommand.Execute(null);
        Assert.True(vm.IsListOpen);
        Assert.Equal(role == UserRole.Employee, vm.IsEmployee);
        Assert.Equal(role == UserRole.Employee, vm.OpenCreateCommand.CanExecute(null));
    }

    [Fact]
    public void ReadGraphResolvesWithoutStockRepositoryAndWithoutOpeningDatabase()
    {
        var services = new ServiceCollection().AddStockFlow(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:StockFlow"] = "Host=127.0.0.1;Port=1;Database=test;Username=test" }).Build());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.NotNull(provider.GetRequiredService<StockMovementQueryService>());
        Assert.IsType<StockRepository>(provider.GetService<IStockRepository>());
    }

    [Fact]
    public async Task EmptyDatabaseNoMatchAndFailureAreDifferentStatesAndCanRetry()
    {
        var f = new Fixture { List = new(Array.Empty<StockMovementDetails>(), false) }; var vm = f.ViewModel();
        await vm.LoadAsync();
        Assert.Equal("Накладних ще немає.", vm.EmptyMessage);
        f.List = f.List with { HasAny = true };
        await vm.LoadAsync();
        Assert.Equal("За обраними фільтрами накладних не знайдено.", vm.EmptyMessage);
        f.ReadError = new IOException("private SQL password");
        await vm.LoadAsync();
        Assert.Null(vm.EmptyMessage);
        Assert.Empty(vm.Movements);
        Assert.Contains("Не вдалося завантажити", vm.ListError);
        Assert.DoesNotContain("password", vm.ListError);
        f.ReadError = null;
        await vm.LoadAsync();
        Assert.Null(vm.ListError);
        Assert.NotNull(vm.EmptyMessage);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task FiltersApplyAutomaticallyAndClearReloadsOnce()
    {
        var f = new Fixture(); var vm = f.ViewModel(); vm.SearchDelay = TimeSpan.Zero;
        vm.SearchNumber = "  нк-01 "; await vm.PendingApply;
        vm.FilterType = MovementType.WriteOff; await vm.PendingApply;
        vm.FilterWarehouseId = 2; await vm.PendingApply;
        vm.DateFrom = new(2026, 11, 1, 12, 0, 0); await vm.PendingApply;
        vm.DateTo = new(2026, 11, 13, 12, 0, 0); await vm.PendingApply;
        Assert.Equal(5, f.ReadCount);
        Assert.Equal(new StockMovementFilter("нк-01", MovementType.WriteOff, 2, new(2026, 11, 1), new(2026, 11, 13)), f.LastFilter);
        await vm.ClearFiltersCommand.ExecuteAsync(null);
        Assert.Equal(6, f.ReadCount);
        Assert.Equal(new StockMovementFilter(""), f.LastFilter);
        Assert.Equal("Усі типи", vm.SelectedFilterType.Name);
        Assert.Equal("Усі склади", vm.SelectedFilterWarehouse.Name);
    }

    [Fact]
    public async Task NumberSearchWaitsForTypingToStop()
    {
        var f = new Fixture(); var vm = f.ViewModel(); vm.SearchDelay = TimeSpan.FromMilliseconds(50);
        vm.SearchNumber = "н"; vm.SearchNumber = "нк"; vm.SearchNumber = "нк-01";
        Assert.Equal(0, f.ReadCount);
        await vm.PendingApply;
        Assert.Equal(1, f.ReadCount);
        Assert.Equal("нк-01", f.LastFilter!.Number);
    }

    [Fact]
    public async Task FilterChangedDuringLoadReloadsWithLatestFilter()
    {
        var f = new Fixture { Gate = new ManualResetEventSlim(false) }; var vm = f.ViewModel();
        var first = vm.LoadAsync();
        Assert.True(vm.IsBusy);
        vm.FilterType = MovementType.Receipt;
        f.Gate.Set();
        await first;
        await vm.PendingApply;
        Assert.Equal(2, f.ReadCount);
        Assert.Equal(MovementType.Receipt, f.LastFilter!.Type);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task FilterChangedWhileDetailsLoadReloadsListWithLatestFilter()
    {
        var f = new Fixture { DetailGate = new ManualResetEventSlim(false) }; var vm = f.ViewModel();
        await vm.LoadAsync();
        var view = vm.ViewCommand.ExecuteAsync(vm.Movements[0]);
        Assert.True(vm.IsBusy);
        vm.FilterType = MovementType.Receipt;
        f.DetailGate.Set();
        await view;
        Assert.Equal(2, f.ReadCount);
        Assert.Equal(MovementType.Receipt, f.LastFilter!.Type);
        Assert.False(vm.IsListOpen);
        vm.BackCommand.Execute(null);
        Assert.True(vm.IsListOpen);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task SearchTypedWhileDetailsFailReloadsListAndKeepsDetailError()
    {
        var f = new Fixture { DetailGate = new ManualResetEventSlim(false), DetailError = new IOException("private query") };
        var vm = f.ViewModel(); vm.SearchDelay = TimeSpan.Zero;
        await vm.LoadAsync();
        var view = vm.ViewCommand.ExecuteAsync(vm.Movements[0]);
        vm.SearchNumber = "нк-01";
        f.DetailGate.Set();
        await view;
        Assert.Equal(2, f.ReadCount);
        Assert.Equal("нк-01", f.LastFilter!.Number);
        Assert.True(vm.IsListOpen);
        Assert.Contains("Не вдалося завантажити деталі", vm.ListError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task InvalidDateRangeShowsErrorWithoutQuery()
    {
        var f = new Fixture(); var vm = f.ViewModel();
        vm.DateFrom = new(2026, 11, 14); await vm.PendingApply;
        var reads = f.ReadCount;
        vm.DateTo = new(2026, 11, 13); await vm.PendingApply;
        Assert.Equal(reads, f.ReadCount);
        Assert.NotNull(vm.ListError);
    }

    [Fact]
    public void InvalidRangeRejectedBeforeRepositoryAndSameDayTimesNormalize()
    {
        var f = new Fixture(); var query = new StockMovementQueryService(f);
        Assert.Throws<ValidationException>(() => query.Search(new(DateFrom: new(2026, 11, 14), DateTo: new(2026, 11, 13))));
        Assert.Equal(0, f.ReadCount);
        query.Search(new(DateFrom: new(2026, 11, 13, 23, 0, 0), DateTo: new(2026, 11, 13)));
        Assert.Equal(new StockMovementFilter(DateFrom: new(2026, 11, 13), DateTo: new(2026, 11, 13)), f.LastFilter);
    }

    [Fact]
    public void InvalidTypeOrWarehouseIsRejectedBeforeQuery()
    {
        var f = new Fixture(); var query = new StockMovementQueryService(f);
        Assert.Throws<ValidationException>(() => query.Search(new(Type: (MovementType)99)));
        Assert.Throws<ValidationException>(() => query.Search(new(WarehouseId: 0)));
        Assert.Equal(0, f.ReadCount);
    }

    [Fact]
    public async Task MissingDetailsAndDetailFailureKeepListAndShowSafeError()
    {
        var f = new Fixture { MissingDetails = true }; var vm = f.ViewModel();
        await vm.LoadAsync();
        await vm.ViewCommand.ExecuteAsync(vm.Movements[0]);
        Assert.True(vm.IsListOpen);
        Assert.Contains("не знайдено", vm.ListError);
        f.DetailError = new IOException("private query");
        await vm.ViewCommand.ExecuteAsync(vm.Movements[0]);
        Assert.True(vm.IsListOpen);
        Assert.Contains("Не вдалося завантажити деталі", vm.ListError);
        Assert.DoesNotContain("private", vm.ListError);
    }

    [Fact]
    public async Task WarehouseFailureDoesNotHideRowsAndCanRetry()
    {
        var f = new Fixture { WarehouseError = true }; var vm = f.ViewModel();
        await vm.LoadAsync();
        Assert.Single(vm.Movements);
        Assert.NotNull(vm.FilterError);
        f.WarehouseError = false;
        await vm.LoadAsync();
        Assert.Null(vm.FilterError);
        Assert.Equal(2, vm.WarehouseOptions.Count);
    }

    [Theory]
    [InlineData(UnitOfMeasure.Piece, "2 шт.")]
    [InlineData(UnitOfMeasure.Kilogram, "2 кг")]
    [InlineData(UnitOfMeasure.Liter, "2 л")]
    public void DisplaySupportsAllUnitsAndOptionalDetails(UnitOfMeasure unit, string text)
    {
        var row = new StockMovementRow(Fixture.Row() with { Type = MovementType.WriteOff, Quantity = 2, Unit = unit, Note = null, SupplierName = null });
        Assert.Equal(text, row.QuantityText);
        Assert.Equal("Списання", row.TypeText);
        Assert.False(row.HasSupplier);
        Assert.Equal("Примітки немає", row.NoteText);
    }

    private sealed class Fixture(UserRole role = UserRole.Employee) : ICurrentUserContext,
        IStockMovementReadRepository, IWarehouseRepository, IProductRepository, ISupplierRepository,
        IStockRepository, IStockMovementRepository, IStockTransactionFactory
    {
        public User CurrentUser { get; } = new() { Id = 7, FullName = "Працівник", Role = role };
        public static StockMovementDetails Row(int id = 1) => new(id, "НК-001", new(2026, 11, 13),
            MovementType.Receipt, "Товар", 5, UnitOfMeasure.Kilogram, "Склад", "Працівник", "Постачальник", "Примітка");
        public StockMovementList List { get; set; } = new(new[] { Row() }, true);
        public StockMovementFilter? LastFilter { get; private set; }
        public Exception? ReadError { get; set; }
        public Exception? DetailError { get; set; }
        public bool WarehouseError { get; set; }
        public int ReadCount { get; private set; }
        public bool MissingDetails { get; set; }
        public ManualResetEventSlim? Gate { get; set; }
        public ManualResetEventSlim? DetailGate { get; set; }
        public StockMovementsViewModel ViewModel() => new(
            new(this), new(this, this), new ProductService(this, this), new SupplierService(this, this),
            new StockMovementService(this, this, this, this), this);
        public StockMovementList Search(StockMovementFilter filter)
        {
            Gate?.Wait();
            ReadCount++; LastFilter = filter;
            if (ReadError is not null) throw ReadError;
            return List;
        }
        public StockMovementDetails? GetById(int id)
        {
            DetailGate?.Wait();
            if (DetailError is not null) throw DetailError;
            return MissingDetails ? null : Row(id);
        }
        public IReadOnlyList<Warehouse> GetAll() => WarehouseError ? throw new IOException() : new[] { new Warehouse { Id = 1, Name = "Склад" } };
        public int Add(Warehouse warehouse) => throw new NotSupportedException();
        IReadOnlyList<Product> IProductRepository.GetAll() => new[] { new Product { Id = 1, Name = "Товар" } };
        int IProductRepository.Add(Product product) => throw new NotSupportedException();
        Product? IProductRepository.GetById(int productId) => throw new NotSupportedException();
        bool IProductRepository.Update(Product product) => throw new NotSupportedException();
        bool IProductRepository.HasRelatedRecords(int productId) => throw new NotSupportedException();
        bool IProductRepository.Delete(int productId) => throw new NotSupportedException();
        IReadOnlyList<Supplier> ISupplierRepository.GetAll() => new[] { new Supplier { Id = 1, Name = "Постачальник" } };
        int ISupplierRepository.Add(Supplier supplier) => throw new NotSupportedException();
        bool ISupplierRepository.Update(Supplier supplier) => throw new NotSupportedException();
        bool ISupplierRepository.Delete(int id) => throw new NotSupportedException();
        Stock IStockRepository.GetForUpdate(int productId, int warehouseId, IDbTransaction transaction) => throw new NotSupportedException();
        void IStockRepository.Save(Stock stock, IDbTransaction transaction) => throw new NotSupportedException();
        IReadOnlyList<Stock> IStockRepository.GetAll(int? warehouseId, IDbTransaction? transaction) => Array.Empty<Stock>();
        int IStockMovementRepository.Add(StockMovement movement, IDbTransaction transaction) => throw new NotSupportedException();
        IStockTransaction IStockTransactionFactory.Begin() => throw new NotSupportedException();
    }
}
