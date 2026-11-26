using Npgsql;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess;
using StockFlow.Domain;

internal static class InvoiceConcurrencyChecks
{
    private static readonly TimeSpan WorkerTimeout = TimeSpan.FromSeconds(10);

    public static async Task RunAsync(DbConnectionFactory factory, IStockRepository stocks, ICurrentUserContext employee,
        Action<bool, string> check)
    {
        var token = "SE7-4-" + Guid.NewGuid().ToString("N")[..16];
        var products = new List<int>();
        var warehouseId = (int)Sql("SELECT min(id) FROM warehouses");
        var supplierId = (int)Sql("SELECT min(id) FROM suppliers");
        var transactions = new NpgsqlStockTransactionFactory(factory);
        var movements = new StockMovementRepository();
        try
        {
            var writeOffProduct = CreateProduct("parallel-write-off", 10);
            var writeOffs = await Race("parallel write-offs",
                Movement("W1", writeOffProduct, MovementType.WriteOff, 8),
                Movement("W2", writeOffProduct, MovementType.WriteOff, 8));
            check(writeOffs.Count(x => x.IsSuccess) == 1 && writeOffs.Any(x => x.Error == "Спробуйте ще раз"),
                "invoice concurrency: exactly one of two 8-from-10 write-offs commits and one gets a serialization refusal");
            check(Quantity(writeOffProduct) == 2 && MovementCount(writeOffProduct) == 1 && Threshold(writeOffProduct) == 3,
                "invoice concurrency: final stock 2, exactly one movement, original threshold unchanged");

            var receiptProduct = CreateProduct("parallel-receipt");
            var receiptRequests = new[]
            {
                Movement("R1", receiptProduct, MovementType.Receipt, 5),
                Movement("R2", receiptProduct, MovementType.Receipt, 7)
            };
            var receipts = await Race("parallel first receipts", receiptRequests[0], receiptRequests[1]);
            check(receipts.Count(x => x.IsSuccess) == 1 && receipts.Any(x => x.Error == "Спробуйте ще раз"),
                "invoice concurrency: parallel first receipts yield one commit and one retry message");
            var committed = receiptRequests[receipts[0].IsSuccess ? 0 : 1];
            check((long)Sql("SELECT count(*) FROM stock WHERE product_id=@p AND warehouse_id=@w", ("p", receiptProduct), ("w", warehouseId)) == 1 &&
                Quantity(receiptProduct) == committed.Quantity && Threshold(receiptProduct) == 0 && MovementCount(receiptProduct) == 1 &&
                (string)Sql("SELECT number FROM stock_movements WHERE product_id=@p", ("p", receiptProduct)) == committed.Number,
                "invoice concurrency: exactly one stock row with the committed quantity and zero threshold; no orphan movement");

            var sequentialProduct = CreateProduct("sequential-write-off", 16);
            var service = new StockMovementService(employee, stocks, movements, transactions);
            var first = service.Register(Movement("S1", sequentialProduct, MovementType.WriteOff, 8));
            var second = service.Register(Movement("S2", sequentialProduct, MovementType.WriteOff, 8));
            check(first.IsSuccess && second.IsSuccess && Quantity(sequentialProduct) == 0 && MovementCount(sequentialProduct) == 2,
                "invoice concurrency control: sequential write-offs both commit, final stock 0");
        }
        finally
        {
            using var connection = factory.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();
            using var command = new NpgsqlCommand("""
                DELETE FROM stock_movements WHERE product_id = ANY(@ids);
                DELETE FROM stock WHERE product_id = ANY(@ids);
                DELETE FROM products WHERE id = ANY(@ids);
                """, connection, tx);
            command.Parameters.AddWithValue("ids", products.ToArray());
            command.ExecuteNonQuery();
            tx.Commit();
        }

        async Task<StockMovementRegistrationResult[]> Race(string scenario, StockMovement first, StockMovement second)
        {
            using var gate = new Barrier(2);
            var boundary = new SynchronizedTransactions(transactions, gate, scenario);
            var service = new StockMovementService(employee, stocks, movements, boundary);
            return await Task.WhenAll(Run(first), Run(second));

            async Task<StockMovementRegistrationResult> Run(StockMovement request)
            {
                try
                {
                    return await Task.Run(() => service.Register(request)).WaitAsync(WorkerTimeout);
                }
                catch (TimeoutException error)
                {
                    throw new TimeoutException($"{scenario}: {request.Number} exceeded the 10-second worker timeout.", error);
                }
            }
        }

        int CreateProduct(string label, int? quantity = null)
        {
            var id = (int)Sql("""
                INSERT INTO products(name, unit, category_id)
                VALUES (@name, 'piece', (SELECT min(id) FROM categories)) RETURNING id
                """, ("name", token + "-" + label));
            products.Add(id);
            if (quantity is { } amount)
                Sql("INSERT INTO stock(product_id, warehouse_id, quantity, min_threshold) VALUES (@p, @w, @q, 3)",
                    ("p", id), ("w", warehouseId), ("q", amount));
            return id;
        }

        StockMovement Movement(string suffix, int product, MovementType type, int quantity) => new()
        {
            Number = token + "-" + suffix, ProductId = product, WarehouseId = warehouseId, Type = type, Quantity = quantity,
            SupplierId = type == MovementType.Receipt ? supplierId : null,
            MovementDate = new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Unspecified)
        };

        int Quantity(int product) => (int)Sql("SELECT quantity FROM stock WHERE product_id=@p AND warehouse_id=@w", ("p", product), ("w", warehouseId));
        int Threshold(int product) => (int)Sql("SELECT min_threshold FROM stock WHERE product_id=@p AND warehouse_id=@w", ("p", product), ("w", warehouseId));
        long MovementCount(int product) => (long)Sql("SELECT count(*) FROM stock_movements WHERE product_id=@p", ("p", product));

        object Sql(string sql, params (string Name, object Value)[] parameters)
        {
            using var connection = factory.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(sql, connection);
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            return command.ExecuteScalar()!;
        }
    }

    private sealed class SynchronizedTransactions(IStockTransactionFactory inner, Barrier gate, string scenario) : IStockTransactionFactory
    {
        public IStockTransaction Begin()
        {
            var scope = inner.Begin();
            try
            {
                // Force a PostgreSQL snapshot in both real transactions before either can lock/update stock.
                using var command = scope.Transaction.Connection!.CreateCommand();
                command.Transaction = scope.Transaction;
                command.CommandTimeout = 5;
                command.CommandText = "SELECT pg_current_snapshot()::text";
                command.ExecuteScalar();
                if (!gate.SignalAndWait(TimeSpan.FromSeconds(6)))
                {
                    Console.Error.WriteLine($"{scenario}: both PostgreSQL snapshots were not established within 6 seconds.");
                    throw new TimeoutException("PostgreSQL snapshot rendezvous timed out.");
                }
                return scope;
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }
    }
}
