using System.Runtime.ExceptionServices;
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
            const string writeOffScenario = "parallel 8-from-10 write-offs";
            var writeOffProduct = CreateProduct("parallel-write-off", 10);
            var writeOffs = await Race(writeOffScenario,
                Movement("W1", writeOffProduct, MovementType.WriteOff, 8),
                Movement("W2", writeOffProduct, MovementType.WriteOff, 8));
            var committedWriteOff = CheckRaceOutcome(writeOffScenario, writeOffs);
            ReportFinalRows(writeOffScenario, writeOffProduct);
            check(Quantity(writeOffProduct) == 2 && MovementCount(writeOffProduct) == 1 && Threshold(writeOffProduct) == 3 &&
                (string)Sql("SELECT number FROM stock_movements WHERE product_id=@p", ("p", writeOffProduct)) == committedWriteOff.Number,
                "invoice concurrency: final stock 2, exactly one committed movement, original threshold unchanged");

            const string receiptScenario = "parallel first receipts";
            var receiptProduct = CreateProduct("parallel-receipt");
            var receipts = await Race(receiptScenario,
                Movement("R1", receiptProduct, MovementType.Receipt, 5),
                Movement("R2", receiptProduct, MovementType.Receipt, 7));
            var committed = CheckRaceOutcome(receiptScenario, receipts);
            ReportFinalRows(receiptScenario, receiptProduct);
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

        async Task<RegistrationAttempt[]> Race(string scenario, StockMovement first, StockMovement second)
        {
            using var gate = new Barrier(2);
            var boundary = new SynchronizedTransactions(transactions, gate, scenario);
            var service = new StockMovementService(employee, stocks, movements, boundary);
            return await Task.WhenAll(Run(first), Run(second));

            async Task<RegistrationAttempt> Run(StockMovement request)
            {
                try
                {
                    return await Task.Run(() => ObserveRegistration(service, request)).WaitAsync(WorkerTimeout);
                }
                catch (TimeoutException error)
                {
                    throw new TimeoutException($"{scenario}: {request.Number} exceeded the 10-second worker timeout.", error);
                }
            }
        }

        StockMovement CheckRaceOutcome(string scenario, IReadOnlyList<RegistrationAttempt> attempts)
        {
            check(attempts.Count(attempt => attempt.Result.IsSuccess) == 1 &&
                attempts.Any(attempt => attempt.Result is { IsSuccess: false, Error: "Спробуйте ще раз" }),
                $"invoice concurrency: {scenario} yields one commit and one retry message");
            var committed = attempts.Single(attempt => attempt.Result.IsSuccess);
            var refused = attempts.Single(attempt => !attempt.Result.IsSuccess);
            var refusedStates = string.Join(", ", refused.SqlStates.Order(StringComparer.Ordinal));
            var committedStates = string.Join(", ", committed.SqlStates.Order(StringComparer.Ordinal));
            check(refused.SqlStates.SetEquals([PostgresErrorCodes.SerializationFailure]) && committed.SqlStates.Count == 0,
                $"invoice concurrency: {scenario} refused {refused.Request.Number} with SQLSTATE [{refusedStates}], committed {committed.Request.Number} with SQLSTATE [{committedStates}]");
            return committed.Request;
        }

        void ReportFinalRows(string scenario, int productId)
        {
            var stockRows = Sql("""
                SELECT COALESCE(json_agg(rows), '[]'::json)::text
                FROM (SELECT product_id, warehouse_id, quantity, min_threshold
                      FROM stock WHERE product_id = @p ORDER BY warehouse_id) AS rows
                """, ("p", productId));
            var movementRows = Sql("""
                SELECT COALESCE(json_agg(rows), '[]'::json)::text
                FROM (SELECT number, type, product_id, warehouse_id, quantity
                      FROM stock_movements WHERE product_id = @p ORDER BY number) AS rows
                """, ("p", productId));
            Console.WriteLine($"invoice concurrency: {scenario} final stock={stockRows}, stock_movements={movementRows}");
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
            Number = token + "-" + suffix,
            ProductId = product,
            WarehouseId = warehouseId,
            Type = type,
            Quantity = quantity,
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

    private static RegistrationAttempt ObserveRegistration(StockMovementService service, StockMovement request)
    {
        var sqlStates = new HashSet<string>(StringComparer.Ordinal);
        var workerId = Environment.CurrentManagedThreadId;

        void RecordSqlState(object? sender, FirstChanceExceptionEventArgs args)
        {
            if (Environment.CurrentManagedThreadId == workerId && args.Exception is PostgresException error)
                sqlStates.Add(error.SqlState);
        }

        // Observe the server exception before Register translates it into the user-facing refusal.
        AppDomain.CurrentDomain.FirstChanceException += RecordSqlState;
        try
        {
            return new RegistrationAttempt(request, service.Register(request), sqlStates);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= RecordSqlState;
        }
    }

    private sealed record RegistrationAttempt(
        StockMovement Request,
        StockMovementRegistrationResult Result,
        IReadOnlySet<string> SqlStates);

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
