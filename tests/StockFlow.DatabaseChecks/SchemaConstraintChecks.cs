using Npgsql;
using StockFlow.DataAccess;

internal static class SchemaConstraintChecks
{
    public static void Run(DbConnectionFactory factory, Action<bool, string> check)
    {
        using var connection = factory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        object? Sql(string sql)
        {
            using var command = new NpgsqlCommand(sql, connection, transaction);
            return command.ExecuteScalar();
        }

        void Reject(string sql, string sqlState, string name, string? constraint = null, string? column = null)
        {
            transaction.Save("schema_case");
            try
            {
                Sql(sql);
                check(false, name + " (statement succeeded)");
            }
            catch (PostgresException error)
            {
                check(error.SqlState == sqlState &&
                    (constraint is null || error.ConstraintName == constraint) &&
                    (column is null || error.ColumnName == column),
                    $"{name} [SQLSTATE {error.SqlState}, {error.ConstraintName ?? error.ColumnName ?? "no constraint name"}]");
            }
            finally
            {
                transaction.Rollback("schema_case");
                transaction.Release("schema_case");
            }
        }

        void Accept(string sql, string name)
        {
            transaction.Save("schema_case");
            try
            {
                check(Sql(sql) is int, name);
            }
            finally
            {
                transaction.Rollback("schema_case");
                transaction.Release("schema_case");
            }
        }

        string Update(string table, string row, string column, string value) =>
            $"UPDATE {table} SET {column} = {value} WHERE {row}";

        void PrimaryKey(string table, int first, int second)
        {
            check(first > 0 && second > 0 && first != second, $"{table}.id SERIAL generates distinct IDs");
            Reject(Update(table, $"id = {second}", "id", first.ToString()),
                PostgresErrorCodes.UniqueViolation, $"{table}_pkey rejects duplicate ID", table + "_pkey");
        }

        void Required(string table, string row, params string[] columns)
        {
            foreach (var column in columns)
                Reject(Update(table, row, column, "NULL"), PostgresErrorCodes.NotNullViolation,
                    $"{table}.{column} rejects NULL", column: column);
        }

        void Length(string table, string row, string column, int max, bool acceptsLimit = true)
        {
            var name = $"{table}.{column} VARCHAR({max})";
            if (acceptsLimit)
                Accept(Update(table, row, column, $"repeat('x', {max})") + " RETURNING id", name + " accepts the limit");
            Reject(Update(table, row, column, $"repeat('x', {max + 1})"),
                PostgresErrorCodes.StringDataRightTruncation, name + " rejects limit plus one");
        }

        void ForeignKey(string table, string row, string column, string parent, int parentId)
        {
            var constraint = $"{table}_{column}_fkey";
            Reject(Update(table, row, column, "-1"), PostgresErrorCodes.ForeignKeyViolation,
                constraint + " rejects missing parent", constraint);
            check(Sql($"SELECT confdeltype = 'r' FROM pg_constraint WHERE conrelid = '{table}'::regclass AND conname = '{constraint}'") is true,
                constraint + " declares ON DELETE RESTRICT");
            Reject($"DELETE FROM {parent} WHERE id = {parentId}", PostgresErrorCodes.ForeignKeyViolation,
                constraint + " blocks parent deletion", constraint);
        }

        // Identifiers and SQL literals below are fixed test inputs. The suffix keeps fixture names unique.
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var category = (int)Sql($"INSERT INTO categories(name) VALUES ('audit-{suffix}') RETURNING id")!;
        var otherCategory = (int)Sql($"INSERT INTO categories(name) VALUES ('other-{suffix}') RETURNING id")!;
        var product = (int)Sql($"INSERT INTO products(name, unit, category_id) VALUES ('Audit stock', 'piece', {category}) RETURNING id")!;
        var otherProduct = (int)Sql($"INSERT INTO products(name, unit, category_id) VALUES ('Audit movement', 'kg', {otherCategory}) RETURNING id")!;
        var warehouse = (int)Sql("INSERT INTO warehouses(name) VALUES ('Audit stock') RETURNING id")!;
        var otherWarehouse = (int)Sql("INSERT INTO warehouses(name, address) VALUES ('Audit movement', 'Address') RETURNING id")!;
        var supplier = (int)Sql("INSERT INTO suppliers(name) VALUES ('Audit supplier') RETURNING id")!;
        var otherSupplier = (int)Sql("INSERT INTO suppliers(name) VALUES ('Other supplier') RETURNING id")!;
        var user = (int)Sql($"INSERT INTO users(username, password_hash, full_name, role) VALUES ('audit-{suffix}', 'hash', 'Audit user', 'employee') RETURNING id")!;
        var otherUser = (int)Sql($"INSERT INTO users(username, password_hash, full_name, role) VALUES ('other-{suffix}', 'hash', 'Other user', 'admin') RETURNING id")!;
        Sql($"INSERT INTO stock(product_id, warehouse_id) VALUES ({product}, {warehouse})");
        var movement = (int)Sql($"""
            INSERT INTO stock_movements(number, type, product_id, warehouse_id, supplier_id, user_id, quantity, movement_date)
            VALUES ('receipt-{suffix}', 'receipt', {otherProduct}, {otherWarehouse}, {supplier}, {user}, 1, CURRENT_TIMESTAMP)
            RETURNING id
            """)!;
        var otherMovement = (int)Sql($"""
            INSERT INTO stock_movements(number, type, product_id, warehouse_id, user_id, quantity, movement_date)
            VALUES ('write-off-{suffix}', 'write_off', {otherProduct}, {otherWarehouse}, {user}, 1, CURRENT_TIMESTAMP)
            RETURNING id
            """)!;

        PrimaryKey("categories", category, otherCategory);
        Required("categories", $"id = {category}", "id", "name");
        Length("categories", $"id = {category}", "name", 100);
        Reject(Update("categories", $"id = {otherCategory}", "name", $"'audit-{suffix}'"),
            PostgresErrorCodes.UniqueViolation, "categories_name_key rejects exact duplicate", "categories_name_key");
        Reject(Update("categories", $"id = {otherCategory}", "name", $"'AUDIT-{suffix}'"),
            PostgresErrorCodes.UniqueViolation, "categories_name_case_insensitive_key rejects case variant", "categories_name_case_insensitive_key");

        PrimaryKey("products", product, otherProduct);
        Required("products", $"id = {product}", "id", "name", "unit", "category_id");
        Length("products", $"id = {product}", "name", 100);
        Length("products", $"id = {product}", "unit", 10, acceptsLimit: false);
        Reject(Update("products", $"id = {product}", "unit", "'box'"),
            PostgresErrorCodes.CheckViolation, "products_unit_check rejects box", "products_unit_check");
        ForeignKey("products", $"id = {product}", "category_id", "categories", category);

        PrimaryKey("warehouses", warehouse, otherWarehouse);
        Required("warehouses", $"id = {warehouse}", "id", "name");
        Length("warehouses", $"id = {warehouse}", "name", 100);
        Length("warehouses", $"id = {warehouse}", "address", 200);
        check(Sql($"SELECT address IS NULL FROM warehouses WHERE id = {warehouse}") is true, "warehouses.address may be NULL");

        PrimaryKey("suppliers", supplier, otherSupplier);
        Required("suppliers", $"id = {supplier}", "id", "name");
        Length("suppliers", $"id = {supplier}", "name", 100);
        Length("suppliers", $"id = {supplier}", "phone", 30);
        Length("suppliers", $"id = {supplier}", "email", 100);

        PrimaryKey("users", user, otherUser);
        Required("users", $"id = {user}", "id", "username", "password_hash", "full_name", "role", "is_active");
        Length("users", $"id = {user}", "username", 50);
        Length("users", $"id = {user}", "password_hash", 255);
        Length("users", $"id = {user}", "full_name", 100);
        Length("users", $"id = {user}", "role", 20, acceptsLimit: false);
        Reject(Update("users", $"id = {otherUser}", "username", $"'audit-{suffix}'"),
            PostgresErrorCodes.UniqueViolation, "users_username_key rejects exact duplicate", "users_username_key");
        Reject(Update("users", $"id = {user}", "role", "'owner'"),
            PostgresErrorCodes.CheckViolation, "users_role_check rejects owner", "users_role_check");
        check(Sql($"SELECT is_active FROM users WHERE id = {user}") is true, "users.is_active defaults to TRUE");

        var stockRow = $"product_id = {product} AND warehouse_id = {warehouse}";
        Required("stock", stockRow, "product_id", "warehouse_id", "quantity", "min_threshold");
        Reject($"INSERT INTO stock(product_id, warehouse_id) VALUES ({product}, {warehouse})",
            PostgresErrorCodes.UniqueViolation, "stock_pkey rejects duplicate pair", "stock_pkey");
        Accept($"INSERT INTO stock(product_id, warehouse_id) VALUES ({product}, {otherWarehouse}) RETURNING product_id",
            "stock_pkey accepts the same product in another warehouse");
        Accept($"INSERT INTO stock(product_id, warehouse_id) VALUES ({otherProduct}, {warehouse}) RETURNING product_id",
            "stock_pkey accepts another product in the same warehouse");
        foreach (var column in new[] { "quantity", "min_threshold" })
        {
            check(Sql($"SELECT {column} FROM stock WHERE {stockRow}") is 0, $"stock.{column} defaults to zero");
            Accept(Update("stock", stockRow, column, "0") + " RETURNING product_id", $"stock.{column} accepts explicit zero");
            Accept(Update("stock", stockRow, column, "2147483647") + " RETURNING product_id", $"stock.{column} accepts INT maximum");
            Reject(Update("stock", stockRow, column, "2147483648"), PostgresErrorCodes.NumericValueOutOfRange,
                $"stock.{column} rejects INT maximum plus one");
            Reject(Update("stock", stockRow, column, "-1"), PostgresErrorCodes.CheckViolation,
                $"stock_{column}_check rejects -1", $"stock_{column}_check");
        }
        ForeignKey("stock", stockRow, "product_id", "products", product);
        ForeignKey("stock", stockRow, "warehouse_id", "warehouses", warehouse);

        var movementRow = $"id = {movement}";
        check(Sql($"SELECT type = 'receipt' AND supplier_id = {supplier} AND quantity = 1 AND note IS NULL FROM stock_movements WHERE {movementRow}") is true,
            "stock_movements accepts receipt with supplier, quantity 1 and NULL note");
        check(Sql($"SELECT type = 'write_off' AND supplier_id IS NULL FROM stock_movements WHERE id = {otherMovement}") is true,
            "stock_movements accepts write_off without supplier");
        PrimaryKey("stock_movements", movement, otherMovement);
        Required("stock_movements", movementRow, "id", "number", "type", "product_id", "warehouse_id", "user_id", "quantity", "movement_date");
        Length("stock_movements", movementRow, "number", 50);
        Length("stock_movements", movementRow, "type", 20, acceptsLimit: false);
        Reject(Update("stock_movements", $"id = {otherMovement}", "number", $"'receipt-{suffix}'"),
            PostgresErrorCodes.UniqueViolation, "stock_movements_number_key rejects duplicate number", "stock_movements_number_key");
        foreach (var invalidQuantity in new[] { "0", "-1" })
            Reject(Update("stock_movements", movementRow, "quantity", invalidQuantity), PostgresErrorCodes.CheckViolation,
                $"stock_movements_quantity_check rejects {invalidQuantity}", "stock_movements_quantity_check");
        Reject(Update("stock_movements", movementRow, "supplier_id", "NULL"),
            PostgresErrorCodes.CheckViolation, "stock_movements_check rejects receipt without supplier", "stock_movements_check");
        Reject(Update("stock_movements", $"id = {otherMovement}", "supplier_id", supplier.ToString()),
            PostgresErrorCodes.CheckViolation, "stock_movements_check rejects write_off with supplier", "stock_movements_check");
        ForeignKey("stock_movements", movementRow, "product_id", "products", otherProduct);
        ForeignKey("stock_movements", movementRow, "warehouse_id", "warehouses", otherWarehouse);
        ForeignKey("stock_movements", movementRow, "supplier_id", "suppliers", supplier);
        ForeignKey("stock_movements", movementRow, "user_id", "users", user);

        // An unknown type also violates the supplier rule. Test the copied type CHECK on its own.
        Sql("CREATE TEMP TABLE movement_type_check (LIKE stock_movements INCLUDING CONSTRAINTS)");
        Sql("ALTER TABLE movement_type_check DROP CONSTRAINT stock_movements_check");
        Sql($"INSERT INTO movement_type_check SELECT * FROM stock_movements WHERE {movementRow}");
        Reject(Update("movement_type_check", movementRow, "type", "'transfer'"), PostgresErrorCodes.CheckViolation,
            "stock_movements_type_check rejects transfer", "stock_movements_type_check");

        transaction.Rollback();
    }
}
