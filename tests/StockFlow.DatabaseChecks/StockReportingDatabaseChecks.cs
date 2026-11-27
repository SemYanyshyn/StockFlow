using Npgsql;
using StockFlow.BusinessLogic;
using StockFlow.DataAccess;

internal static class StockReportingDatabaseChecks
{
    public static void Run(DbConnectionFactory factory, Action<bool, string> check)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var productName = "SA7-4 product " + token;
        var firstWarehouseName = "SA7-4 warehouse 1 " + token;
        var secondWarehouseName = "SA7-4 warehouse 2 " + token;
        var numberPrefix = "SA7-4-" + token;
        var dateFrom = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Unspecified);
        var dateTo = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Unspecified);
        var service = new StockReportingQueryService(new StockReportingQueryRepository(factory));

        try
        {
            var categoryId = ScalarInt("SELECT min(id) FROM categories");
            var supplierId = ScalarInt("SELECT min(id) FROM suppliers");
            var userId = ScalarInt("SELECT id FROM users WHERE username = 'employee'");
            var productId = ScalarInt(
                "INSERT INTO products(name, unit, category_id) VALUES (@name, 'piece', @category) RETURNING id",
                ("name", productName), ("category", categoryId));
            var firstWarehouseId = ScalarInt(
                "INSERT INTO warehouses(name) VALUES (@name) RETURNING id",
                ("name", firstWarehouseName));
            var secondWarehouseId = ScalarInt(
                "INSERT INTO warehouses(name) VALUES (@name) RETURNING id",
                ("name", secondWarehouseName));

            Execute(
                """
                INSERT INTO stock(product_id, warehouse_id, quantity, min_threshold)
                VALUES (@product, @warehouse1, 7, 2), (@product, @warehouse2, 6, 2);
                """,
                ("product", productId), ("warehouse1", firstWarehouseId), ("warehouse2", secondWarehouseId));

            Execute(
                """
                INSERT INTO stock_movements(
                    number, type, product_id, warehouse_id, supplier_id, user_id, quantity, movement_date)
                VALUES
                    (@number1, 'receipt', @product, @warehouse1, @supplier, @user, 100, @beforePeriod),
                    (@number2, 'receipt', @product, @warehouse1, @supplier, @user, 10, @periodStart),
                    (@number3, 'write_off', @product, @warehouse1, NULL, @user, 3, @lastMicrosecond),
                    (@number4, 'write_off', @product, @warehouse1, NULL, @user, 100, @afterPeriod),
                    (@number5, 'receipt', @product, @warehouse2, @supplier, @user, 8, @secondWarehouseReceipt),
                    (@number6, 'write_off', @product, @warehouse2, NULL, @user, 2, @secondWarehouseWriteOff);
                """,
                ("number1", numberPrefix + "-before"),
                ("number2", numberPrefix + "-start"),
                ("number3", numberPrefix + "-end"),
                ("number4", numberPrefix + "-after"),
                ("number5", numberPrefix + "-warehouse2-receipt"),
                ("number6", numberPrefix + "-warehouse2-write-off"),
                ("product", productId),
                ("warehouse1", firstWarehouseId),
                ("warehouse2", secondWarehouseId),
                ("supplier", supplierId),
                ("user", userId),
                ("beforePeriod", dateFrom.AddDays(-1)),
                ("periodStart", dateFrom),
                ("lastMicrosecond", dateTo.AddDays(1).AddMicroseconds(-1)),
                ("afterPeriod", dateTo.AddDays(1)),
                ("secondWarehouseReceipt", dateFrom.AddDays(1).AddHours(9)),
                ("secondWarehouseWriteOff", dateFrom.AddDays(1).AddHours(11)));

            var results = service.GetMovementSummary(dateFrom, dateTo)
                .Where(row => row.ProductName == productName)
                .ToArray();

            var firstWarehouse = results.SingleOrDefault(row => row.WarehouseName == firstWarehouseName);
            var secondWarehouse = results.SingleOrDefault(row => row.WarehouseName == secondWarehouseName);
            check(firstWarehouse is not null &&
                  firstWarehouse.IncomingQuantity == 10 &&
                  firstWarehouse.OutgoingQuantity == 3 &&
                  firstWarehouse.CurrentBalance == 7,
                "Звіт за період відсікає рухи поза межами та повертає точні суми першого складу.");
            check(secondWarehouse is not null &&
                  secondWarehouse.IncomingQuantity == 8 &&
                  secondWarehouse.OutgoingQuantity == 2 &&
                  secondWarehouse.CurrentBalance == 6 &&
                  results.Length == 2,
                "Звіт окремо групує рухи другого складу та повертає точні надходження, списання й залишок.");
        }
        finally
        {
            using var connection = factory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            ExecuteInTransaction(
                "DELETE FROM stock_movements WHERE product_id IN (SELECT id FROM products WHERE name = @productName)",
                ("productName", productName));
            ExecuteInTransaction(
                "DELETE FROM stock WHERE product_id IN (SELECT id FROM products WHERE name = @productName)",
                ("productName", productName));
            ExecuteInTransaction("DELETE FROM products WHERE name = @productName", ("productName", productName));
            ExecuteInTransaction(
                "DELETE FROM warehouses WHERE name IN (@firstName, @secondName)",
                ("firstName", firstWarehouseName), ("secondName", secondWarehouseName));
            transaction.Commit();

            void ExecuteInTransaction(string sql, params (string Name, object Value)[] parameters)
            {
                using var command = new NpgsqlCommand(sql, connection, transaction);
                foreach (var parameter in parameters)
                    command.Parameters.AddWithValue(parameter.Name, parameter.Value);
                command.ExecuteNonQuery();
            }
        }

        int ScalarInt(string sql, params (string Name, object Value)[] parameters)
        {
            using var connection = factory.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(sql, connection);
            foreach (var parameter in parameters)
                command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        void Execute(string sql, params (string Name, object Value)[] parameters)
        {
            using var connection = factory.CreateConnection();
            connection.Open();
            using var command = new NpgsqlCommand(sql, connection);
            foreach (var parameter in parameters)
                command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            command.ExecuteNonQuery();
        }
    }
}
