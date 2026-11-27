using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Products;
using StockFlow.DataAccess;
using Xunit;

namespace StockFlow.Tests;

public sealed class StockReportingQueryServiceTests
{
    [Fact]
    public void GetMovementSummary_ПеревернутийПеріод_КидаєValidationExceptionБезВикликівРепозиторію()
    {
        var repository = new FakeStockReportingQueryRepository();
        var service = new StockReportingQueryService(repository);

        var error = Assert.Throws<ValidationException>(() =>
            service.GetMovementSummary(new DateTime(2026, 11, 14), new DateTime(2026, 11, 13)));

        Assert.Equal("Дата початку не може бути пізнішою за кінцеву дату.", error.Message);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public void GetMovementSummary_ОдинДень_ВключаєРухО2359()
    {
        var date = new DateTime(2026, 11, 13);
        var repository = new FakeStockReportingQueryRepository();
        repository.Movements.Add(new Movement("Сир", "кг", "Склад 1", 5, true, date.AddHours(23).AddMinutes(59)));
        var service = new StockReportingQueryService(repository);

        var summary = service.GetMovementSummary(date, date);

        var row = Assert.Single(summary);
        Assert.Equal(5, row.IncomingQuantity);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public void GetMovementSummary_ПочатокТаОстанняМікросекунда_ВключаєОбидваРухи()
    {
        var dateFrom = new DateTime(2026, 11, 12);
        var dateTo = new DateTime(2026, 11, 13);
        var repository = new FakeStockReportingQueryRepository();
        repository.Movements.Add(new Movement("Сир", "кг", "Склад 1", 4, true, dateFrom));
        repository.Movements.Add(new Movement(
            "Сир", "кг", "Склад 1", 6, true,
            dateTo.AddDays(1).AddMicroseconds(-1)));
        var service = new StockReportingQueryService(repository);

        var row = Assert.Single(service.GetMovementSummary(dateFrom, dateTo));

        Assert.Equal(10, row.IncomingQuantity);
    }

    [Fact]
    public void GetMovementSummary_РухПозамежамиПеріоду_ВиключаєОбидваРухи()
    {
        var dateFrom = new DateTime(2026, 11, 12);
        var dateTo = new DateTime(2026, 11, 13);
        var repository = new FakeStockReportingQueryRepository();
        repository.Movements.Add(new Movement(
            "Сир", "кг", "Склад 1", 4, true,
            dateFrom.AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59)));
        repository.Movements.Add(new Movement("Сир", "кг", "Склад 1", 6, true, dateTo.AddDays(1)));
        var service = new StockReportingQueryService(repository);

        var summary = service.GetMovementSummary(dateFrom, dateTo);

        Assert.Empty(summary);
    }

    [Fact]
    public void GetMovementSummary_ПеріодБезРухів_ПовертаєПорожнійРезультат()
    {
        var repository = new FakeStockReportingQueryRepository();
        var service = new StockReportingQueryService(repository);

        var summary = service.GetMovementSummary(new DateTime(2026, 11, 12), new DateTime(2026, 11, 13));

        Assert.Empty(summary);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public void GetMovementSummary_ДваСклади_ГрупуєЗаТоваромІСкладомІПовертаєТочніПідсумки()
    {
        var date = new DateTime(2026, 11, 13);
        var repository = new FakeStockReportingQueryRepository();
        repository.Movements.Add(new Movement("Сир", "кг", "Склад 1", 10, true, date.AddHours(9)));
        repository.Movements.Add(new Movement("Сир", "кг", "Склад 1", 2, false, date.AddHours(10)));
        repository.Movements.Add(new Movement("Сир", "кг", "Склад 2", 7, true, date.AddHours(11)));
        repository.Movements.Add(new Movement("Сир", "кг", "Склад 2", 3, false, date.AddHours(12)));
        var service = new StockReportingQueryService(repository);

        var summary = service.GetMovementSummary(date, date);

        Assert.Equal(2, summary.Count);
        var firstWarehouse = Assert.Single(summary, row => row.WarehouseName == "Склад 1");
        var secondWarehouse = Assert.Single(summary, row => row.WarehouseName == "Склад 2");
        Assert.Equal((10, 2, 8), (
            firstWarehouse.IncomingQuantity,
            firstWarehouse.OutgoingQuantity,
            firstWarehouse.CurrentBalance));
        Assert.Equal((7, 3, 4), (
            secondWarehouse.IncomingQuantity,
            secondWarehouse.OutgoingQuantity,
            secondWarehouse.CurrentBalance));
    }

    [Fact]
    public void GetMovementSummary_БагатоденнийОстаннійДень_ВключаєРухО2359()
    {
        var dateFrom = new DateTime(2026, 11, 10);
        var dateTo = new DateTime(2026, 11, 12);
        var repository = new FakeStockReportingQueryRepository();
        repository.Movements.Add(new Movement(
            "Сир", "кг", "Склад 1", 9, true,
            dateTo.AddHours(23).AddMinutes(59)));
        var service = new StockReportingQueryService(repository);

        var row = Assert.Single(service.GetMovementSummary(dateFrom, dateTo));

        Assert.Equal(9, row.IncomingQuantity);
    }

    [Theory]
    [InlineData(5, 5, false)]
    [InlineData(0, 0, false)]
    [InlineData(4, 5, true)]
    public void GetStockBalances_ПорігІКількість_ПовертаєОчікуванийСтанНизькогоЗапасу(
        int quantity,
        int minThreshold,
        bool expectedIsLow)
    {
        var repository = new FakeStockReportingQueryRepository();
        repository.Balances.Add(new StockBalanceQueryRow(
            "Сир", "кг", "Склад 1", quantity, minThreshold));
        var service = new StockReportingQueryService(repository);

        var balance = Assert.Single(service.GetStockBalances());

        Assert.Equal(expectedIsLow, balance.IsLow);
    }

    private sealed class FakeStockReportingQueryRepository : IStockReportingQueryRepository
    {
        public List<StockBalanceQueryRow> Balances { get; } = [];
        public List<Movement> Movements { get; } = [];
        public int Calls { get; private set; }

        public IReadOnlyList<StockBalanceQueryRow> GetStockBalances() => Balances;

        public IReadOnlyList<MovementSummaryQueryRow> GetMovementSummary(DateTime? dateFrom, DateTime? dateTo)
        {
            Calls++;
            var from = dateFrom?.Date;
            var toExclusive = dateTo?.Date.AddDays(1);
            var included = Movements.Where(movement =>
                (!from.HasValue || movement.MovementDate >= from.Value) &&
                (!toExclusive.HasValue || movement.MovementDate < toExclusive.Value));

            return included
                .GroupBy(movement => (movement.ProductName, movement.Unit, movement.WarehouseName))
                .Select(group => new MovementSummaryQueryRow(
                    ProductName: group.Key.ProductName,
                    Unit: group.Key.Unit,
                    WarehouseName: group.Key.WarehouseName,
                    IncomingQuantity: group.Where(movement => movement.IsIncoming).Sum(movement => movement.Quantity),
                    OutgoingQuantity: group.Where(movement => !movement.IsIncoming).Sum(movement => movement.Quantity),
                    CurrentBalance:
                        group.Where(movement => movement.IsIncoming).Sum(movement => movement.Quantity) -
                        group.Where(movement => !movement.IsIncoming).Sum(movement => movement.Quantity)))
                .ToArray();
        }
    }

    private sealed record Movement(
        string ProductName,
        string Unit,
        string WarehouseName,
        int Quantity,
        bool IsIncoming,
        DateTime MovementDate);
}
