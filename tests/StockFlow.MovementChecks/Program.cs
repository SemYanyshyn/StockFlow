using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess;
using StockFlow.Domain;

// Only fixture SQL lives here. No test stock adapter, no production configuration.
var value = Environment.GetEnvironmentVariable("STOCKFLOW_TEST_CONNECTION")
    ?? throw new InvalidOperationException("Set STOCKFLOW_TEST_CONNECTION to a fresh, isolated schema+seed database.");
var settings = new NpgsqlConnectionStringBuilder(value);
if (settings.Host is not ("127.0.0.1" or "localhost") ||
    !settings.Database!.StartsWith("stockflow_isolated_", StringComparison.Ordinal))
    throw new InvalidOperationException("Only a loopback stockflow_isolated_* database is permitted.");
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["ConnectionStrings:StockFlow"] = value }).Build();
using var provider = new ServiceCollection().AddStockFlow(config).BuildServiceProvider(
    new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
var factory = provider.GetRequiredService<DbConnectionFactory>();
var query = provider.GetRequiredService<StockMovementQueryService>();
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAILED: " + message);
    checks++; Console.WriteLine("PASS " + message);
}
void Execute(string sql)
{
    using var connection = factory.CreateConnection(); connection.Open();
    using var command = new NpgsqlCommand(sql, connection); command.ExecuteNonQuery();
}
Check(!query.Search(new()).HasAny, "empty database is distinct from no matches");
Check(provider.GetService<IStockRepository>() is null, "read DI does not require a stock repository");
// Real joined fixture rows, not application demo data. Exercise midnight and microsecond boundaries.
Execute("""
    INSERT INTO stock_movements(number,type,product_id,warehouse_id,supplier_id,user_id,quantity,movement_date,note) VALUES
      ('НК-016','receipt',3,1,1,2,5,'2026-11-13 00:00:00','Перевірка примітки'),
      ('НК-017','write_off',9,2,NULL,2,2,'2026-11-13 23:59:59.999999',NULL),
      ('НК-018','receipt',1,1,2,2,4,'2026-11-14 00:00:00','Наступний день');
    """);
var all = query.Search(new());
Check(all.HasAny && all.Items.Count == 3, "all joined movements load");
Check(all.Items.Select(m => m.Number).SequenceEqual(new[] { "НК-018", "НК-017", "НК-016" }), "newest first with stable order");
var day = query.Search(new(DateFrom: new(2026, 11, 13), DateTo: new(2026, 11, 13)));
Check(day.Items.Count == 2, "inclusive end day: midnight and 23:59:59.999999 included, next midnight excluded");
Check(query.Search(new(Type: MovementType.Receipt)).Items.Count == 2, "type filter");
Check(query.Search(new(WarehouseId: 2)).Items.Single().Number == "НК-017", "warehouse filter");
Check(query.Search(new("нк-016", MovementType.Receipt, 1, new(2026, 11, 13), new(2026, 11, 13))).Items.Count == 1,
    "combined filters and Cyrillic case-insensitive number search");
Check(query.Search(new("' OR 1=1 --")).Items.Count == 0 && query.Search(new("%")).Items.Count == 0,
    "number search is parameterized and literal");
var noMatch = query.Search(new(Number: "absent"));
Check(noMatch.HasAny && noMatch.Items.Count == 0, "no matches retains database presence");
var receipt = query.GetById(all.Items.Single(m => m.Number == "НК-016").Id);
Check(receipt.SupplierName == "ТОВ «Молочна ферма»" && receipt.Note == "Перевірка примітки" &&
      receipt.ProductName == "Сир твердий" && receipt.Unit == UnitOfMeasure.Kilogram && receipt.ResponsibleName == "Працівник складу",
      "receipt detail includes real supplier, note, product unit and current directory user name");
var writeOff = query.GetById(all.Items.Single(m => m.Number == "НК-017").Id);
Check(writeOff.SupplierName is null && writeOff.Note is null && writeOff.Unit == UnitOfMeasure.Piece,
    "write-off detail maps SQL NULL fields and piece unit");
Check(all.Items[0].Unit == UnitOfMeasure.Liter, "liter unit mapping");
foreach (var login in new[] { "admin", "employee" })
{
    provider.GetRequiredService<AuthService>().Login(login, login == "admin" ? "AdminDemo!2026" : "EmployeeDemo!2026");
    Check(query.Search(new()).Items.Count == 3, login + " reads the list through application services");
}
Console.WriteLine($"All {checks} PostgreSQL invoice list checks passed. Creation is out of this branch scope.");
