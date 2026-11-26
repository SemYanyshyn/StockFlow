using Npgsql;
using StockFlow.DataAccess;
using StockFlow.Domain;

internal static class InvoiceDatabaseChecks
{
    public static void Run(DbConnectionFactory factory, Action<bool, string> check)
    {
        using var connection = factory.CreateConnection();
        connection.Open();
        var token = "SE7-2-" + Guid.NewGuid().ToString("N")[..16];
        var date = new DateTime(2026, 11, 13, 23, 59, 59, DateTimeKind.Unspecified);
        var warehouseId = (int)Scalar("SELECT min(id) FROM warehouses");
        var supplierId = (int)Scalar("SELECT min(id) FROM suppliers");
        var userId = (int)Scalar("SELECT id FROM users WHERE username = 'employee'");
        var productId = (int)Scalar("""
            INSERT INTO products(name, unit, category_id)
            VALUES (@name, 'liter', (SELECT min(id) FROM categories)) RETURNING id
            """, ("name", token));
        var writer = new StockMovementRepository();
        var reader = new StockMovementReadRepository(factory);

        try
        {
            int receiptId;
            int writeOffId;
            using (var transaction = connection.BeginTransaction())
            {
                receiptId = writer.Add(Movement("receipt", MovementType.Receipt, supplierId), transaction);
                writeOffId = writer.Add(Movement("write-off", MovementType.WriteOff, null), transaction);
                transaction.Commit();
            }

            var receipt = reader.GetById(receiptId);
            var writeOff = reader.GetById(writeOffId);
            check(receiptId > 0 && receipt is { SupplierName: not null } && receipt.Id == receiptId && receipt.Number == token + "-receipt",
                "invoice repository: receipt with supplier persists and returns generated id");
            check(writeOffId > 0 && writeOffId != receiptId &&
                (string)Scalar("SELECT type FROM stock_movements WHERE id = @id", ("id", receiptId)) == "receipt" &&
                (string)Scalar("SELECT type FROM stock_movements WHERE id = @id", ("id", writeOffId)) == "write_off" &&
                receipt?.Type == MovementType.Receipt && writeOff?.Type == MovementType.WriteOff,
                "invoice repository: both types round-trip through PostgreSQL");
            check(writeOff is { SupplierName: null, Note: null } && writeOff.MovementDate == date &&
                (bool)Scalar("SELECT supplier_id IS NULL AND note IS NULL FROM stock_movements WHERE id = @id", ("id", writeOffId)),
                "invoice repository: SQL NULL supplier/note and timestamp round-trip");

            Reject(Movement("no-supplier", MovementType.Receipt, null), PostgresErrorCodes.CheckViolation, "receipt without supplier CHECK");
            Reject(Movement("has-supplier", MovementType.WriteOff, supplierId), PostgresErrorCodes.CheckViolation, "write-off with supplier CHECK");
            Reject(Movement("zero", MovementType.Receipt, supplierId, quantity: 0), PostgresErrorCodes.CheckViolation, "zero quantity CHECK");
            Reject(Movement("bad-product", MovementType.Receipt, supplierId, product: -1), PostgresErrorCodes.ForeignKeyViolation, "missing product FK");
            Reject(Movement("bad-warehouse", MovementType.Receipt, supplierId, warehouse: -1), PostgresErrorCodes.ForeignKeyViolation, "missing warehouse FK");
            Reject(Movement("bad-user", MovementType.Receipt, supplierId, user: -1), PostgresErrorCodes.ForeignKeyViolation, "missing user FK");

            Expect<DuplicateNumberException>(() => Insert(Movement("receipt", MovementType.Receipt, supplierId)),
                error => error.Message == "Номер накладної вже існує", "duplicate number is translated");
            Expect<PostgresException>(() => Scalar("DELETE FROM products WHERE id = @id RETURNING id", ("id", productId)),
                error => error.SqlState == PostgresErrorCodes.ForeignKeyViolation && error.ConstraintName == "stock_movements_product_id_fkey",
                "product with a movement cannot be deleted (RESTRICT)");
            Expect<InvalidOperationException>(() => StockMovementTypeColumn.Parse((string)Scalar("SELECT 'future_type'")),
                error => error.Message == "Невідомий тип накладної.", "unknown database type is rejected by the read mapper");
        }
        finally
        {
            using var cleanup = connection.BeginTransaction();
            using (var command = new NpgsqlCommand("DELETE FROM stock_movements WHERE product_id = @id; DELETE FROM products WHERE id = @id;", connection, cleanup))
            {
                command.Parameters.AddWithValue("id", productId);
                command.ExecuteNonQuery();
            }
            cleanup.Commit();
        }

        StockMovement Movement(string name, MovementType type, int? supplier, int quantity = 4,
            int? product = null, int? warehouse = null, int? user = null) => new()
            {
                Number = token + "-" + name,
                Type = type,
                SupplierId = supplier,
                Quantity = quantity,
                ProductId = product ?? productId,
                WarehouseId = warehouse ?? warehouseId,
                UserId = user ?? userId,
                MovementDate = date
            };

        void Insert(StockMovement movement)
        {
            using var transaction = connection.BeginTransaction();
            writer.Add(movement, transaction);
            transaction.Rollback();
        }

        void Reject(StockMovement movement, string sqlState, string description) =>
            Expect<PostgresException>(() => Insert(movement), error => error.SqlState == sqlState, description);

        void Expect<T>(Action action, Func<T, bool> matches, string description) where T : Exception
        {
            try { action(); }
            catch (T error)
            {
                check(matches(error), "invoice repository: " + description);
                return;
            }
            check(false, "invoice repository: " + description);
        }

        object Scalar(string sql, params (string Name, object Value)[] parameters)
        {
            using var command = new NpgsqlCommand(sql, connection);
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            return command.ExecuteScalar()!;
        }
    }
}
