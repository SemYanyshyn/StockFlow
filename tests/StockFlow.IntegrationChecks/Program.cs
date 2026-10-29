using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess;
using StockFlow.Domain;

// Explicit manual integration runner, outside the normal Solution/CI.
// It requires an independently created disposable local database, never a user's working database.
var input = Environment.GetEnvironmentVariable("STOCKFLOW_INTEGRATION_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(input))
    throw new InvalidOperationException("Set STOCKFLOW_INTEGRATION_CONNECTION_STRING for a disposable local test database.");
var settings = new NpgsqlConnectionStringBuilder(input);
if (settings.Host != "127.0.0.1" ||
    !settings.Database!.StartsWith("stockflow_se6_test_", StringComparison.Ordinal))
    throw new InvalidOperationException("Only 127.0.0.1 and a stockflow_se6_test_* database are permitted.");
settings.Pooling = false;
settings.ApplicationName = "StockFlowStage6Prototype";
var connectionString = settings.ConnectionString;
using var probe = new NpgsqlConnection(connectionString);
probe.Open();

int ReadInt(string sql, params (string Name, object Value)[] parameters)
{
    using var cmd = new NpgsqlCommand(sql, probe);
    foreach (var parameter in parameters) cmd.Parameters.AddWithValue(parameter.Name, parameter.Value);
    return Convert.ToInt32(cmd.ExecuteScalar());
}
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("Integration assertion failed: " + description);
}
var product = ReadInt("SELECT id FROM products ORDER BY id LIMIT 1");
var warehouse = ReadInt("SELECT id FROM warehouses ORDER BY id LIMIT 1");
var supplier = ReadInt("SELECT id FROM suppliers ORDER BY id LIMIT 1");
var employee = ReadInt("SELECT id FROM users WHERE role='employee' ORDER BY id LIMIT 1");
var stamp = Guid.NewGuid().ToString("N")[..8];

StockMovement Movement(string suffix, MovementType type = MovementType.Receipt, int quantity = 5) => new()
{
    Number = "НК-TEST-" + stamp + "-" + suffix,
    Type = type,
    ProductId = product,
    WarehouseId = warehouse,
    SupplierId = type == MovementType.Receipt ? supplier : null,
    UserId = employee,
    Quantity = quantity,
    MovementDate = new DateTime(2026, 10, 30, 12, 34, 56, DateTimeKind.Utc),
    Note = "Параметр '; SELECT 1; -- перевірка"
};
var repository = new StockMovementRepository();

// Parameter/null/TIMESTAMP/RETURNING checks against the team's real schema.
using (var tx = probe.BeginTransaction())
{
    var movement = Movement("mapping");
    var id = repository.Add(movement, tx);
    using (var cmd = new NpgsqlCommand("SELECT number,type,product_id,warehouse_id,supplier_id,user_id,quantity,movement_date,note FROM stock_movements WHERE id=@id", probe, tx))
    {
        cmd.Parameters.AddWithValue("id", id);
        using var row = cmd.ExecuteReader();
        Check(row.Read(), "RETURNING id identifies inserted movement");
        Check(row.GetString(0) == movement.Number && row.GetString(1) == "receipt", "number and explicit receipt mapping");
        Check(row.GetInt32(2) == product && row.GetInt32(3) == warehouse && row.GetInt32(4) == supplier && row.GetInt32(5) == employee, "foreign key values");
        Check(row.GetInt32(6) == 5 && row.GetString(8) == movement.Note, "parameterized quantity/note retained");
        Check(row.GetDateTime(7) == DateTime.SpecifyKind(movement.MovementDate, DateTimeKind.Unspecified), "TIMESTAMP wall-clock value");
    }
    var writeOff = Movement("nullable", MovementType.WriteOff, 1);
    writeOff.Note = null;
    var nullId = repository.Add(writeOff, tx);
    using (var cmd = new NpgsqlCommand("SELECT type,supplier_id,note FROM stock_movements WHERE id=@id", probe, tx))
    {
        cmd.Parameters.AddWithValue("id", nullId);
        using var row = cmd.ExecuteReader();
        Check(row.Read() && row.GetString(0) == "write_off" && row.IsDBNull(1) && row.IsDBNull(2), "nullable supplier/note and explicit write_off mapping");
    }
    tx.Rollback();
}
Console.WriteLine("PASS repository: real INSERT/RETURNING, all parameters, nullable fields and TIMESTAMP mapping");

// Verify exact PostgreSQL unique constraint and safe translation.
using (var tx = probe.BeginTransaction())
{
    var movement = Movement("duplicate");
    repository.Add(movement, tx);
    var translated = false;
    try { repository.Add(movement, tx); }
    catch (DuplicateNumberException error)
    {
        translated = error.InnerException is null;
    }
    Check(translated, "number constraint becomes DuplicateNumberException without provider details");
    tx.Rollback();
}
Console.WriteLine("PASS repository: actual stock_movements_number_key is classified correctly");

void SetStock(int quantity)
{
    using var cmd = new NpgsqlCommand("INSERT INTO stock(product_id,warehouse_id,quantity,min_threshold) VALUES (@p,@w,@q,4) ON CONFLICT(product_id,warehouse_id) DO UPDATE SET quantity=EXCLUDED.quantity,min_threshold=4", probe);
    cmd.Parameters.AddWithValue("p", product);
    cmd.Parameters.AddWithValue("w", warehouse);
    cmd.Parameters.AddWithValue("q", quantity);
    cmd.ExecuteNonQuery();
}
int StockQuantity() => ReadInt("SELECT quantity FROM stock WHERE product_id=@p AND warehouse_id=@w", ("p", product), ("w", warehouse));
int MovementCount(string number)
{
    using var cmd = new NpgsqlCommand("SELECT count(*) FROM stock_movements WHERE number=@n", probe);
    cmd.Parameters.AddWithValue("n", number);
    return Convert.ToInt32(cmd.ExecuteScalar());
}

var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
{
    ["ConnectionStrings:StockFlow"] = connectionString
}).Build();
var factory = new NpgsqlStockTransactionFactory(new DbConnectionFactory(configuration));
StockMovementService Service(bool failSave = false) => new(new TestUser(employee), new SqlStockAdapter(failSave), repository, factory);

SetStock(10);
var receipt = Movement("commit");
receipt.UserId = int.MaxValue; // must be ignored in favor of trusted current context
var result = Service().Register(receipt);
Check(result.IsSuccess && result.MovementId > 0, "service commit succeeds");
Check(StockQuantity() == 15 && MovementCount(receipt.Number) == 1, "real movement and balance commit together");
Check(ReadInt("SELECT min_threshold FROM stock WHERE product_id=@p AND warehouse_id=@w", ("p", product), ("w", warehouse)) == 4, "threshold preserved");
Check(ReadInt("SELECT user_id FROM stock_movements WHERE id=@id", ("id", result.MovementId!.Value)) == employee, "trusted user is stored");
Console.WriteLine("PASS service: real Serializable transaction commits movement+quantity and preserves threshold/user identity");

SetStock(10);
var failing = Movement("rollback");
var failed = Service(failSave: true).Register(failing);
Check(!failed.IsSuccess && StockQuantity() == 10 && MovementCount(failing.Number) == 0, "real inserted movement rolled back after Save CHECK failure");
Console.WriteLine("PASS service: injected real SQL CHECK failure in test Save rolls back movement and stock");

var insufficient = Movement("insufficient", MovementType.WriteOff, 11);
var denied = Service().Register(insufficient);
Check(!denied.IsSuccess && denied.Error == "Недостатньо товару на складі", "domain error preserved");
Check(StockQuantity() == 10 && MovementCount(insufficient.Number) == 0, "rejected writeoff leaves database unchanged");
Console.WriteLine("PASS service: writeoff over stock leaves no movement and no quantity change");

var duplicate = Service().Register(receipt);
Check(!duplicate.IsSuccess && duplicate.Error == "Номер накладної вже існує" && MovementCount(receipt.Number) == 1 && StockQuantity() == 10, "duplicate does not commit second movement or quantity");
Console.WriteLine("PASS service: real duplicate number returns sanitized message and rolls back");

Check(ReadInt("SELECT count(*) FROM pg_stat_activity WHERE application_name='StockFlowStage6Prototype' AND pid<>pg_backend_pid()") == 0, "operation connections disposed");
Console.WriteLine("PASS resources: no operation connections remain after success or failure");
Console.WriteLine("Integration groups passed: 7. These tests use a TEST-ONLY stock SQL adapter, not Sasha's production repository.");

file sealed class TestUser(int id) : ICurrentUserContext
{
    public User CurrentUser { get; } = new() { Id = id, Role = UserRole.Employee };
}

// Integration-only adapter to exercise the candidate transaction service with actual PostgreSQL.
// It is not Sasha's implementation and must never be moved into production.
file sealed class SqlStockAdapter(bool failSave) : IStockRepository
{
    public Stock GetForUpdate(int productId, int warehouseId, IDbTransaction transaction)
    {
        if (transaction.IsolationLevel != IsolationLevel.Serializable) throw new InvalidOperationException("Expected Serializable");
        using var cmd = transaction.Connection!.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT quantity,min_threshold FROM stock WHERE product_id=@p AND warehouse_id=@w FOR UPDATE";
        Parameter(cmd,"p",productId); Parameter(cmd,"w",warehouseId);
        using var row = cmd.ExecuteReader();
        return row.Read()
            ? new Stock { ProductId=productId,WarehouseId=warehouseId,Quantity=row.GetInt32(0),MinThreshold=row.GetInt32(1) }
            : new Stock { ProductId=productId,WarehouseId=warehouseId,Quantity=0,MinThreshold=0 };
    }

    public void Save(Stock stock, IDbTransaction transaction)
    {
        using var cmd = transaction.Connection!.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = failSave
            ? "UPDATE stock SET quantity=-1 WHERE product_id=@p AND warehouse_id=@w"
            : "INSERT INTO stock(product_id,warehouse_id,quantity,min_threshold) VALUES(@p,@w,@q,0) ON CONFLICT(product_id,warehouse_id) DO UPDATE SET quantity=EXCLUDED.quantity";
        Parameter(cmd,"p",stock.ProductId); Parameter(cmd,"w",stock.WarehouseId);
        if (!failSave) Parameter(cmd,"q",stock.Quantity);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<Stock> GetAll(int? warehouseId) => throw new NotSupportedException("Not used by this manual test");

    private static void Parameter(IDbCommand cmd,string name,int value)
    {
        var parameter=cmd.CreateParameter(); parameter.ParameterName=name; parameter.Value=value; parameter.DbType=DbType.Int32;
        cmd.Parameters.Add(parameter);
    }
}
