namespace StockFlow.DataAccess;

public sealed record StockBalanceQueryRow(
    string ProductName,
    string Unit,
    string WarehouseName,
    int Quantity,
    int MinThreshold);

public sealed record MovementSummaryQueryRow(
    string ProductName,
    string Unit,
    string WarehouseName,
    int IncomingQuantity,
    int OutgoingQuantity,
    int CurrentBalance);

public interface IStockReportingQueryRepository
{
    IReadOnlyList<StockBalanceQueryRow> GetStockBalances();
    IReadOnlyList<MovementSummaryQueryRow> GetMovementSummary(DateTime? dateFrom, DateTime? dateTo);
}
