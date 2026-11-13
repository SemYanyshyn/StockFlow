using System.Data;
using Npgsql;
using NpgsqlTypes;
using StockFlow.DataAccess.Products;
using StockFlow.Domain;

namespace StockFlow.DataAccess;

/// <summary>Reading never resolves the stock repository or the registration service.</summary>
public sealed class StockMovementReadRepository(DbConnectionFactory connections) : IStockMovementReadRepository
{
    private const string Projection = """
        SELECT m.id, m.number, m.movement_date, m.type, p.name, m.quantity, p.unit,
               w.name, u.full_name, s.name, m.note
        FROM stock_movements m
        JOIN products p ON p.id = m.product_id
        JOIN warehouses w ON w.id = m.warehouse_id
        JOIN users u ON u.id = m.user_id
        LEFT JOIN suppliers s ON s.id = m.supplier_id
        """;

    public StockMovementList Search(StockMovementFilter filter)
    {
        using var connection = connections.CreateConnection();
        connection.Open();
        // The empty-state check and the rows describe the same database snapshot.
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        using var command = new NpgsqlCommand(Projection + " WHERE TRUE", connection, transaction);
        if (!string.IsNullOrEmpty(filter.Number))
        {
            // Literal substring: '%' and '_' are characters, not wildcard operators.
            command.CommandText += " AND strpos(lower(m.number COLLATE stockflow_case_insensitive) COLLATE \"C\", lower(@number COLLATE stockflow_case_insensitive) COLLATE \"C\") > 0";
            command.Parameters.AddWithValue("number", NpgsqlDbType.Text, filter.Number);
        }
        if (filter.Type is { } type)
        {
            command.CommandText += " AND m.type = @type";
            command.Parameters.AddWithValue("type", type == MovementType.Receipt ? "receipt" : "write_off");
        }
        if (filter.WarehouseId is { } warehouse)
        {
            command.CommandText += " AND m.warehouse_id = @warehouse";
            command.Parameters.AddWithValue("warehouse", warehouse);
        }
        if (filter.DateFrom is { } from)
        {
            command.CommandText += " AND m.movement_date >= @dateFrom";
            command.Parameters.AddWithValue("dateFrom", NpgsqlDbType.Date, from.Date);
        }
        if (filter.DateTo is { } to)
        {
            // Exclusive midnight after the selected day includes every timestamp that day.
            command.CommandText += " AND m.movement_date < @dateTo + INTERVAL '1 day'";
            command.Parameters.AddWithValue("dateTo", NpgsqlDbType.Date, to.Date);
        }
        command.CommandText += " ORDER BY m.movement_date DESC, m.id DESC";
        var items = new List<StockMovementDetails>();
        using (var reader = command.ExecuteReader())
            while (reader.Read()) items.Add(Read(reader));

        var hasAny = items.Count != 0;
        if (!hasAny)
        {
            using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM stock_movements)", connection, transaction);
            hasAny = (bool)exists.ExecuteScalar()!;
        }
        transaction.Commit();
        return new(items, hasAny);
    }

    public StockMovementDetails? GetById(int id)
    {
        using var connection = connections.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand(Projection + " WHERE m.id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    private static StockMovementDetails Read(NpgsqlDataReader reader) => new(
        reader.GetInt32(0), reader.GetString(1), reader.GetDateTime(2),
        reader.GetString(3) switch
        {
            "receipt" => MovementType.Receipt,
            "write_off" => MovementType.WriteOff,
            _ => throw new InvalidOperationException("Невідомий тип накладної.")
        },
        reader.GetString(4), reader.GetInt32(5), UnitOfMeasureColumn.Parse(reader.GetString(6)),
        reader.GetString(7), reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10));
}
