using System;
using System.Collections.Generic;
using StockFlow.BusinessLogic.Products;
using StockFlow.DataAccess;

namespace StockFlow.BusinessLogic;

public sealed record StockBalanceDto(
    string ProductName,
    string Unit,
    string WarehouseName,
    int Quantity,
    int MinThreshold,
    bool IsLow);

public sealed record MovementReportDto(
    string ProductName,
    string Unit,
    string WarehouseName,
    int IncomingQuantity,
    int OutgoingQuantity,
    int CurrentBalance);

public sealed class StockReportingQueryService
{
    private readonly IStockReportingQueryRepository _repository;

    public StockReportingQueryService(IStockReportingQueryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public IReadOnlyList<StockBalanceDto> GetStockBalances()
    {
        var rows = _repository.GetStockBalances();
        var result = new List<StockBalanceDto>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(new StockBalanceDto(
                ProductName: row.ProductName,
                Unit: row.Unit,
                WarehouseName: row.WarehouseName,
                Quantity: row.Quantity,
                MinThreshold: row.MinThreshold,
                IsLow: StockService.IsBelowThreshold(row.Quantity, row.MinThreshold)));
        }

        return result;
    }

    public IReadOnlyList<MovementReportDto> GetMovementSummary(DateTime? dateFrom, DateTime? dateTo)
    {
        var from = dateFrom?.Date;
        var to = dateTo?.Date;
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ValidationException("Дата початку не може бути пізнішою за кінцеву дату.");

        var rows = _repository.GetMovementSummary(from, to);
        var result = new List<MovementReportDto>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(new MovementReportDto(
                ProductName: row.ProductName,
                Unit: row.Unit,
                WarehouseName: row.WarehouseName,
                IncomingQuantity: row.IncomingQuantity,
                OutgoingQuantity: row.OutgoingQuantity,
                CurrentBalance: row.CurrentBalance));
        }

        return result;
    }
}
