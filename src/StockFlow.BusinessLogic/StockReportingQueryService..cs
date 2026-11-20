using System;
using System.Collections.Generic;
using System.Data;
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
    private readonly DbConnectionFactory _connectionFactory;

    public StockReportingQueryService(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public IReadOnlyList<StockBalanceDto> GetStockBalances()
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.name AS product_name,
                   p.unit AS unit,
                   w.name AS warehouse_name,
                   s.quantity AS quantity,
                   s.min_threshold AS min_threshold
            FROM stock s
            JOIN products p ON p.id = s.product_id
            JOIN warehouses w ON w.id = s.warehouse_id
            ORDER BY p.name, w.name;
            """;

        var result = new List<StockBalanceDto>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var quantity = Convert.ToInt32(reader["quantity"]);
            var minThreshold = Convert.ToInt32(reader["min_threshold"]);
            var isLow = minThreshold > 0 ? quantity <= minThreshold : quantity == 0;

            result.Add(new StockBalanceDto(
                ProductName: Convert.ToString(reader["product_name"]) ?? string.Empty,
                Unit: Convert.ToString(reader["unit"]) ?? "piece",
                WarehouseName: Convert.ToString(reader["warehouse_name"]) ?? string.Empty,
                Quantity: quantity,
                MinThreshold: minThreshold,
                IsLow: isLow));
        }

        return result;
    }

    public IReadOnlyList<MovementReportDto> GetMovementSummary(DateTime? dateFrom, DateTime? dateTo)
    {
        if (dateFrom?.Date > dateTo?.Date)
            throw new ValidationException("Дата початку не може бути пізнішою за кінцеву дату.");

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var command = connection.CreateCommand();
        var conditions = new List<string>();

        if (dateFrom.HasValue)
        {
            conditions.Add("sm.movement_date >= @dateFrom");
            AddParameter(command, "@dateFrom", dateFrom.Value.Date);
        }

        if (dateTo.HasValue)
        {
            conditions.Add("sm.movement_date < @dateTo");
            AddParameter(command, "@dateTo", dateTo.Value.Date.AddDays(1));
        }

        var whereClause = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : string.Empty;

        command.CommandText = $"""
            SELECT p.name AS product_name,
                   p.unit AS unit,
                   w.name AS warehouse_name,
                   COALESCE(SUM(CASE WHEN sm.type = 'receipt' THEN sm.quantity ELSE 0 END), 0) AS incoming_qty,
                   COALESCE(SUM(CASE WHEN sm.type = 'write_off' THEN sm.quantity ELSE 0 END), 0) AS outgoing_qty,
                   COALESCE(MAX(st.quantity), 0) AS current_balance
            FROM stock_movements sm
            JOIN products p ON p.id = sm.product_id
            JOIN warehouses w ON w.id = sm.warehouse_id
            LEFT JOIN stock st ON st.product_id = sm.product_id AND st.warehouse_id = sm.warehouse_id
            {whereClause}
            GROUP BY p.id, p.name, p.unit, w.id, w.name
            ORDER BY p.name, w.name;
            """;

        var result = new List<MovementReportDto>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new MovementReportDto(
                ProductName: Convert.ToString(reader["product_name"]) ?? string.Empty,
                Unit: Convert.ToString(reader["unit"]) ?? "piece",
                WarehouseName: Convert.ToString(reader["warehouse_name"]) ?? string.Empty,
                IncomingQuantity: Convert.ToInt32(reader["incoming_qty"]),
                OutgoingQuantity: Convert.ToInt32(reader["outgoing_qty"]),
                CurrentBalance: Convert.ToInt32(reader["current_balance"])));
        }

        return result;
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}