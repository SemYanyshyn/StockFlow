using System;
using System.Collections.Generic;
using System.Data;
using StockFlow.Domain;

namespace StockFlow.DataAccess;

public sealed class StockRepository : IStockRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public StockRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public Stock GetForUpdate(int productId, int warehouseId, IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var connection = transaction.Connection
            ?? throw new InvalidOperationException("Транзакція не прив'язана до активного з'єднання.");

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT product_id, warehouse_id, quantity, min_threshold
            FROM stock
            WHERE product_id = @productId AND warehouse_id = @warehouseId
            FOR UPDATE;
            """;

        AddParameter(command, "@productId", productId);
        AddParameter(command, "@warehouseId", warehouseId);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new Stock
            {
                ProductId = Convert.ToInt32(reader["product_id"]),
                WarehouseId = Convert.ToInt32(reader["warehouse_id"]),
                Quantity = Convert.ToInt32(reader["quantity"]),
                MinThreshold = Convert.ToInt32(reader["min_threshold"])
            };
        }

        return new Stock
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Quantity = 0,
            MinThreshold = 0
        };
    }

    public void Save(Stock stock, IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(transaction);
        var connection = transaction.Connection
            ?? throw new InvalidOperationException("Транзакція не прив'язана до активного з'єднання.");

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO stock (product_id, warehouse_id, quantity, min_threshold)
            VALUES (@productId, @warehouseId, @quantity, @minThreshold)
            ON CONFLICT (product_id, warehouse_id)
            DO UPDATE SET quantity = EXCLUDED.quantity;
            """;

        AddParameter(command, "@productId", stock.ProductId);
        AddParameter(command, "@warehouseId", stock.WarehouseId);
        AddParameter(command, "@quantity", stock.Quantity);
        AddParameter(command, "@minThreshold", stock.MinThreshold);

        command.ExecuteNonQuery();
    }

    public IReadOnlyList<Stock> GetAll(int? warehouseId = null, IDbTransaction? transaction = null)
    {
        var ownsConnection = transaction is null;
        var connection = transaction?.Connection ?? _connectionFactory.CreateConnection();

        try
        {
            if (ownsConnection && connection.State != ConnectionState.Open)
                connection.Open();

            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            if (warehouseId.HasValue)
            {
                command.CommandText = """
                    SELECT product_id, warehouse_id, quantity, min_threshold
                    FROM stock
                    WHERE warehouse_id = @warehouseId
                    ORDER BY product_id, warehouse_id;
                    """;
                AddParameter(command, "@warehouseId", warehouseId.Value);
            }
            else
            {
                command.CommandText = """
                    SELECT product_id, warehouse_id, quantity, min_threshold
                    FROM stock
                    ORDER BY product_id, warehouse_id;
                    """;
            }

            var items = new List<Stock>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new Stock
                {
                    ProductId = Convert.ToInt32(reader["product_id"]),
                    WarehouseId = Convert.ToInt32(reader["warehouse_id"]),
                    Quantity = Convert.ToInt32(reader["quantity"]),
                    MinThreshold = Convert.ToInt32(reader["min_threshold"])
                });
            }

            return items;
        }
        finally
        {
            if (ownsConnection)
                connection.Dispose();
        }
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}