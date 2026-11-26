using System.Data;

namespace StockFlow.DataAccess;

public sealed class StockReportingQueryRepository : IStockReportingQueryRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public StockReportingQueryRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public IReadOnlyList<StockBalanceQueryRow> GetStockBalances()
    {
        using var connection = _connectionFactory.CreateConnection();
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

        var result = new List<StockBalanceQueryRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new StockBalanceQueryRow(
                ProductName: Convert.ToString(reader["product_name"]) ?? string.Empty,
                Unit: Convert.ToString(reader["unit"]) ?? "piece",
                WarehouseName: Convert.ToString(reader["warehouse_name"]) ?? string.Empty,
                Quantity: Convert.ToInt32(reader["quantity"]),
                MinThreshold: Convert.ToInt32(reader["min_threshold"])));
        }

        return result;
    }

    public IReadOnlyList<MovementSummaryQueryRow> GetMovementSummary(DateTime? dateFrom, DateTime? dateTo)
    {
        using var connection = _connectionFactory.CreateConnection();
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

        var result = new List<MovementSummaryQueryRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new MovementSummaryQueryRow(
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
