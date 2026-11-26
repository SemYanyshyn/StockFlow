using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using StockFlow.DataAccess;
using StockFlow.Domain;

internal static class StockRepositoryChecks
{
    public static void Run(DbConnectionFactory factory, Action<bool, string> check)
    {
        var repo = new StockRepository(factory);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        int ScalarInt(string sql, params (string Name, object Value)[] parameters)
        {
            using var conn = factory.CreateConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            foreach (var p in parameters)
                cmd.Parameters.AddWithValue(p.Name, p.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        void Execute(string sql, params (string Name, object Value)[] parameters)
        {
            using var conn = factory.CreateConnection();
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            foreach (var p in parameters)
                cmd.Parameters.AddWithValue(p.Name, p.Value);
            cmd.ExecuteNonQuery();
        }

        var categoryId = ScalarInt(
            "INSERT INTO categories(name) VALUES (@name) RETURNING id",
            ("name", "StockCat " + suffix));
        var product1Id = ScalarInt(
            "INSERT INTO products(name, unit, category_id) VALUES (@name, 'piece', @cat) RETURNING id",
            ("name", "StockProd1 " + suffix), ("cat", categoryId));
        var product2Id = ScalarInt(
            "INSERT INTO products(name, unit, category_id) VALUES (@name, 'kg', @cat) RETURNING id",
            ("name", "StockProd2 " + suffix), ("cat", categoryId));
        var warehouse1Id = ScalarInt(
            "INSERT INTO warehouses(name) VALUES (@name) RETURNING id",
            ("name", "StockWh1 " + suffix));
        var warehouse2Id = ScalarInt(
            "INSERT INTO warehouses(name) VALUES (@name) RETURNING id",
            ("name", "StockWh2 " + suffix));

        try
        {
            // 1. GetForUpdate on missing record returns zero Stock without inserting a row
            using (var conn = factory.CreateConnection())
            {
                conn.Open();
                using var tx = conn.BeginTransaction();
                var missing = repo.GetForUpdate(product1Id, warehouse1Id, tx);
                tx.Commit();

                check(missing.ProductId == product1Id &&
                      missing.WarehouseId == warehouse1Id &&
                      missing.Quantity == 0 &&
                      missing.MinThreshold == 0,
                    "StockRepository.GetForUpdate returns zero Stock when missing");
            }
            check(ScalarInt("SELECT count(*) FROM stock WHERE product_id=@p AND warehouse_id=@w",
                    ("p", product1Id), ("w", warehouse1Id)) == 0,
                "StockRepository.GetForUpdate does not insert row when missing");

            // 2. Save inserts a new row when (product_id, warehouse_id) does not exist
            using (var conn = factory.CreateConnection())
            {
                conn.Open();
                using var tx = conn.BeginTransaction();
                repo.Save(new Stock
                {
                    ProductId = product1Id,
                    WarehouseId = warehouse1Id,
                    Quantity = 15,
                    MinThreshold = 4
                }, tx);
                tx.Commit();
            }
            check(ScalarInt("SELECT quantity FROM stock WHERE product_id=@p AND warehouse_id=@w",
                    ("p", product1Id), ("w", warehouse1Id)) == 15 &&
                  ScalarInt("SELECT min_threshold FROM stock WHERE product_id=@p AND warehouse_id=@w",
                    ("p", product1Id), ("w", warehouse1Id)) == 4,
                "StockRepository.Save creates new stock row");

            // 3. GetForUpdate on existing record returns stored quantity and threshold
            using (var conn = factory.CreateConnection())
            {
                conn.Open();
                using var tx = conn.BeginTransaction();
                var existing = repo.GetForUpdate(product1Id, warehouse1Id, tx);
                tx.Commit();

                check(existing.Quantity == 15 && existing.MinThreshold == 4,
                    "StockRepository.GetForUpdate returns stored quantity and threshold");
            }

            // 4. Save on existing record updates quantity without overwriting min_threshold
            using (var conn = factory.CreateConnection())
            {
                conn.Open();
                using var tx = conn.BeginTransaction();
                repo.Save(new Stock
                {
                    ProductId = product1Id,
                    WarehouseId = warehouse1Id,
                    Quantity = 42,
                    MinThreshold = 999
                }, tx);
                tx.Commit();
            }
            check(ScalarInt("SELECT quantity FROM stock WHERE product_id=@p AND warehouse_id=@w",
                    ("p", product1Id), ("w", warehouse1Id)) == 42 &&
                  ScalarInt("SELECT min_threshold FROM stock WHERE product_id=@p AND warehouse_id=@w",
                    ("p", product1Id), ("w", warehouse1Id)) == 4,
                "StockRepository.Save updates quantity without overwriting min_threshold");

            // 5. Row locking: second GetForUpdate blocks until first transaction commits (10s timeout)
            using (var lockConn1 = factory.CreateConnection())
            {
                lockConn1.Open();
                using var lockTx1 = lockConn1.BeginTransaction();
                var locked = repo.GetForUpdate(product1Id, warehouse1Id, lockTx1);
                locked.Quantity = 50;
                repo.Save(locked, lockTx1);

                using var workerStarted = new ManualResetEventSlim(false);
                var readerTask = Task.Run(() =>
                {
                    using var lockConn2 = factory.CreateConnection();
                    lockConn2.Open();
                    using var lockTx2 = lockConn2.BeginTransaction();
                    workerStarted.Set();
                    var afterCommit = repo.GetForUpdate(product1Id, warehouse1Id, lockTx2);
                    lockTx2.Commit();
                    return afterCommit;
                });

                if (!workerStarted.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Second connection did not start within 10 seconds.");

                var completedBeforeCommit = Task.WhenAny(readerTask, Task.Delay(250)).GetAwaiter().GetResult() == readerTask;
                check(!completedBeforeCommit, "StockRepository.GetForUpdate blocks second connection until COMMIT");

                lockTx1.Commit();
                var unblocked = readerTask.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                check(unblocked.Quantity == 50 && unblocked.MinThreshold == 4,
                    "StockRepository.GetForUpdate unblocks after COMMIT and sees updated quantity");
            }

            // 6. Negative quantity rejected by CHECK constraint
            try
            {
                using var badConn = factory.CreateConnection();
                badConn.Open();
                using var badTx = badConn.BeginTransaction();
                repo.Save(new Stock
                {
                    ProductId = product1Id,
                    WarehouseId = warehouse1Id,
                    Quantity = -1,
                    MinThreshold = 0
                }, badTx);
                badTx.Commit();
                check(false, "stock.quantity < 0 rejected by CHECK constraint");
            }
            catch (PostgresException ex)
            {
                check(ex.SqlState == PostgresErrorCodes.CheckViolation,
                    "stock.quantity < 0 rejected by CHECK constraint (23514)");
            }

            // 7. Duplicate (product_id, warehouse_id) rejected by PRIMARY KEY
            try
            {
                Execute("INSERT INTO stock(product_id, warehouse_id, quantity, min_threshold) VALUES (@p, @w, 1, 0)",
                    ("p", product1Id), ("w", warehouse1Id));
                check(false, "stock duplicate (product_id, warehouse_id) rejected by PRIMARY KEY");
            }
            catch (PostgresException ex)
            {
                check(ex.SqlState == PostgresErrorCodes.UniqueViolation,
                    "stock duplicate (product_id, warehouse_id) rejected by PRIMARY KEY (23505)");
            }

            // 8. GetAll filters by warehouse and returns empty list for empty table
            using (var conn = factory.CreateConnection())
            {
                conn.Open();
                using var tx = conn.BeginTransaction();
                repo.Save(new Stock
                {
                    ProductId = product2Id,
                    WarehouseId = warehouse2Id,
                    Quantity = 7,
                    MinThreshold = 2
                }, tx);
                tx.Commit();
            }

            var filtered = repo.GetAll(warehouse1Id);
            check(filtered.Count == 1 &&
                  filtered[0].ProductId == product1Id &&
                  filtered[0].WarehouseId == warehouse1Id,
                "StockRepository.GetAll(warehouseId) returns only rows of selected warehouse");

            using (var emptyConn = factory.CreateConnection())
            {
                emptyConn.Open();
                using var emptyTx = emptyConn.BeginTransaction();
                using (var delCmd = new NpgsqlCommand("DELETE FROM stock;", emptyConn, emptyTx))
                    delCmd.ExecuteNonQuery();

                var emptyList = repo.GetAll(transaction: emptyTx);
                check(emptyList.Count == 0, "StockRepository.GetAll returns empty list when table is empty");
                emptyTx.Rollback();
            }
        }
        finally
        {
            Execute("DELETE FROM stock WHERE product_id IN (@p1, @p2)", ("p1", product1Id), ("p2", product2Id));
            Execute("DELETE FROM products WHERE id IN (@p1, @p2)", ("p1", product1Id), ("p2", product2Id));
            Execute("DELETE FROM warehouses WHERE id IN (@w1, @w2)", ("w1", warehouse1Id), ("w2", warehouse2Id));
            Execute("DELETE FROM categories WHERE id = @cat", ("cat", categoryId));
        }
    }
}
