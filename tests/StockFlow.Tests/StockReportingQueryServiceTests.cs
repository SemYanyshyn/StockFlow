using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Products;
using StockFlow.DataAccess;
using Xunit;

namespace StockFlow.Tests;

public sealed class StockReportingQueryServiceTests
{
    [Fact]
    public void GetMovementSummary_ReversedPeriod_ThrowsBeforeRepositoryCall()
    {
        var repository = new FakeStockReportingQueryRepository();
        var service = new StockReportingQueryService(repository);

        var error = Assert.Throws<ValidationException>(() =>
            service.GetMovementSummary(new DateTime(2026, 11, 14), new DateTime(2026, 11, 13)));

        Assert.Equal("Дата початку не може бути пізнішою за кінцеву дату.", error.Message);
        Assert.Equal(0, repository.MovementSummaryCalls);
    }

    [Fact]
    public void GetMovementSummary_LastMicrosecondIncludedAndNextMidnightExcluded()
    {
        var dateTo = new DateTime(2026, 11, 13);
        var repository = new FakeStockReportingQueryRepository();
        repository.Movements.Add(new Movement(
            "Сир", "кг", "Склад 1", 5, true,
            dateTo.Date.AddDays(1).AddMicroseconds(-1)));
        repository.Movements.Add(new Movement(
            "Сир", "кг", "Склад 1", 8, true,
            dateTo.Date.AddDays(1)));
        var service = new StockReportingQueryService(repository);

        var summary = service.GetMovementSummary(dateTo.AddDays(-1), dateTo);

        var row = Assert.Single(summary);
        Assert.Equal(5, row.IncomingQuantity);
        Assert.Equal(0, row.OutgoingQuantity);
        Assert.Equal(1, repository.MovementSummaryCalls);
    }

    [Fact]
    public void GetMovementSummary_GroupsIncomingAndOutgoingByWarehouse()
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
        Assert.Equal((10, 2), (firstWarehouse.IncomingQuantity, firstWarehouse.OutgoingQuantity));
        Assert.Equal((7, 3), (secondWarehouse.IncomingQuantity, secondWarehouse.OutgoingQuantity));
    }

    [Theory]
    [InlineData(5, 5, false)]
    [InlineData(0, 0, false)]
    [InlineData(4, 5, true)]
    public void GetStockBalances_LowStockRequiresPositiveThresholdAndQuantityBelowIt(
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
        public int MovementSummaryCalls { get; private set; }

        public IReadOnlyList<StockBalanceQueryRow> GetStockBalances() => Balances;

        public IReadOnlyList<MovementSummaryQueryRow> GetMovementSummary(DateTime? dateFrom, DateTime? dateTo)
        {
            MovementSummaryCalls++;
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
                    CurrentBalance: 0))
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
