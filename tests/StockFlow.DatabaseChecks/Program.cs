using Microsoft.Extensions.Configuration;
using Npgsql;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Suppliers;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess;
using StockFlow.DataAccess.Products;
using StockFlow.DataAccess.Suppliers;
using StockFlow.DataAccess.Users;
using StockFlow.Domain;

// Deliberately outside StockFlow.sln/CI. Only an explicitly named isolated database is accepted.
var connectionString = Environment.GetEnvironmentVariable("STOCKFLOW_TEST_CONNECTION") ??
    throw new InvalidOperationException("Set STOCKFLOW_TEST_CONNECTION to an isolated, schema+seed initialized database.");
var settings = new NpgsqlConnectionStringBuilder(connectionString);
if (settings.Host is not ("127.0.0.1" or "localhost") ||
    !settings.Database!.StartsWith("stockflow_isolated_", StringComparison.Ordinal))
    throw new InvalidOperationException("Database checks require loopback and a stockflow_isolated_* database.");
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{ ["ConnectionStrings:StockFlow"] = connectionString }).Build();
var factory = new DbConnectionFactory(configuration);
var users = new UserRepository(factory);
var hasher = new Pbkdf2PasswordHasher();
var adminContext = new AuthenticatedUserContext();
var adminAuth = new AuthService(() => users, hasher, adminContext);
adminAuth.Login("admin", "AdminDemo!2026");
var adminId = ((ICurrentUserContext)adminContext).UserId;
var userService = new UserService(users, adminContext, hasher);
var supplierRepository = new SupplierRepository(factory);
var suppliers = new SupplierService(supplierRepository, adminContext);
var suffix = Guid.NewGuid().ToString("N")[..8];
var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("FAILED: " + description);
    checks++; Console.WriteLine("PASS " + description);
}
void ExpectValidation(Action action, string message)
{
    try { action(); throw new InvalidOperationException("Expected validation failure"); }
    catch (ValidationException error) { Check(error.Message == message, message); }
}
object Scalar(string sql, params (string Name, object Value)[] parameters)
{
    using var connection = factory.CreateConnection(); connection.Open();
    using var command = new NpgsqlCommand(sql, connection);
    foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
    return command.ExecuteScalar()!;
}
void Execute(string sql, params (string Name, object Value)[] parameters)
{
    using var connection = factory.CreateConnection(); connection.Open();
    using var command = new NpgsqlCommand(sql, connection);
    foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
    command.ExecuteNonQuery();
}

Check(((ICurrentUserContext)adminContext).IsAdmin && adminId > 0, "seed admin login and role");
var employeeContext = new AuthenticatedUserContext();
var employeeAuth = new AuthService(() => users, hasher, employeeContext);
employeeAuth.Login("employee", "EmployeeDemo!2026");
var employeeId = ((ICurrentUserContext)employeeContext).UserId;
Check(!((ICurrentUserContext)employeeContext).IsAdmin, "seed employee login and role");
ExpectValidation(() => employeeAuth.Login("unknown", "wrong"), AuthService.InvalidCredentialsMessage);
userService.SetActive(employeeId, false);
ExpectValidation(() => employeeAuth.Login("employee", "wrong"), AuthService.InvalidCredentialsMessage);
ExpectValidation(() => employeeAuth.Login("employee", "EmployeeDemo!2026"), AuthService.BlockedMessage);
userService.SetActive(employeeId, true);
employeeAuth.Login("employee", "EmployeeDemo!2026");
Check(employeeContext.IsAuthenticated, "unblocking restores login");
ExpectValidation(() => new UserService(users, employeeContext, hasher).GetAll(), UserService.AdminOnlyMessage);
ExpectValidation(() => userService.SetActive(adminId, false), UserService.SelfBlockMessage);
ExpectValidation(() => userService.Delete(adminId), UserService.SelfDeleteMessage);
ExpectValidation(() => userService.Update(adminId, "Admin", UserRole.Employee), UserService.LastAdminMessage);

var supplier = suppliers.Add("Supplier " + suffix, "123", "a@b");
suppliers.Update(supplier.Id, " Оновлений " + suffix + " ", " ", " ");
var savedSupplier = supplierRepository.GetAll().Single(s => s.Id == supplier.Id);
Check(savedSupplier.Phone is null && savedSupplier.Email is null && savedSupplier.Name == "Оновлений " + suffix, "supplier SQL update, Trim and NULL mapping");
Check((bool)Scalar("SELECT phone IS NULL AND email IS NULL FROM suppliers WHERE id=@id", ("id", supplier.Id)), "optional contacts are SQL NULL");
var user = userService.Create("dbcheck_" + suffix, " Іван Перевірка ", "DbCheck!2026", UserRole.Employee);
var storedUser = users.GetByUsername(user.Username)!;
Check(storedUser.PasswordHash != "DbCheck!2026" && hasher.Verify("DbCheck!2026", storedUser.PasswordHash), "user password persisted only as PBKDF2 hash");
Check(userService.GetAll().All(u => u.PasswordHash.Length == 0), "user list contains no password hashes");
ExpectValidation(() => userService.Create(user.Username, "Duplicate", "DbCheck!2026", UserRole.Employee), UserService.DuplicateMessage);
var differentCase = userService.Create(user.Username.ToUpperInvariant(), "Case-sensitive identity", "DbCheck!2026", UserRole.Employee);
Check(differentCase.Id != user.Id, "existing case-sensitive UNIQUE(username) rule preserved");
userService.Update(user.Id, " Оновлений Користувач ", UserRole.Admin);
storedUser = users.GetByUsername(user.Username)!;
Check(storedUser.FullName == "Оновлений Користувач" && storedUser.Role == UserRole.Admin && hasher.Verify("DbCheck!2026", storedUser.PasswordHash), "edit preserves login and password");
Check(userService.Filter(userService.GetAll(), "ОНОВЛЕНИЙ", UserRole.Admin).Any(u => u.Id == user.Id), "Cyrillic search and role filter");
userService.Update(user.Id, "Name", UserRole.Employee);
Execute("INSERT INTO stock_movements(number,type,product_id,warehouse_id,supplier_id,user_id,quantity,movement_date) VALUES(@number,'receipt',(SELECT min(id) FROM products),(SELECT min(id) FROM warehouses),@supplier,@user,1,CURRENT_TIMESTAMP)",
    ("number", "check_" + suffix), ("supplier", supplier.Id), ("user", user.Id));
try { suppliers.Delete(supplier.Id); throw new InvalidOperationException("Expected supplier FK rejection"); }
catch (ValidationException error) { Check(error.Message.Contains("рухами"), "supplier FK 23503 translated by service"); }
ExpectValidation(() => userService.Delete(user.Id), UserService.InUseMessage);
using (var transaction = users.BeginManagement())
{
    try { transaction.Delete(user.Id); throw new InvalidOperationException("Expected user FK rejection"); }
    catch (RecordInUseException) { Check(true, "user FK 23503 translated as secondary protection"); }
}
var unused = suppliers.Add("unused " + suffix);
suppliers.Delete(unused.Id);
Check(!supplierRepository.GetAll().Any(s => s.Id == unused.Id), "unused supplier removed");
userService.Delete(differentCase.Id);
Check(users.GetByUsername(differentCase.Username) is null, "unused user removed");

var categories = new CategoryRepository(factory);
try { categories.Add(new Category { Name = "напої" }); throw new InvalidOperationException("Expected case-insensitive duplicate"); }
catch (DuplicateNameException) { Check(true, "ICU unique index rejects Напої/напої in locale C"); }
Check((bool)Scalar("SELECT 'ЇЄІҐ' COLLATE stockflow_case_insensitive = 'їєіґ' COLLATE stockflow_case_insensitive"), "Ukrainian Cyrillic case mapping");
Check(!(bool)Scalar("SELECT 'ї' COLLATE stockflow_case_insensitive = 'і' COLLATE stockflow_case_insensitive"), "ICU keeps Ukrainian accents distinct");

// Every pair starts with exactly two active admins. Both threads begin together, on independent connections.
foreach (var operation in new[] { "demote", "block", "delete" })
{
    var first = userService.Create(operation + "_a_" + suffix, "First", "Concurrent!2026", UserRole.Admin);
    var second = userService.Create(operation + "_b_" + suffix, "Second", "Concurrent!2026", UserRole.Admin);
    var contexts = new[] { new AuthenticatedUserContext(), new AuthenticatedUserContext() };
    new AuthService(() => users, hasher, contexts[0]).Login(first.Username, "Concurrent!2026");
    new AuthService(() => users, hasher, contexts[1]).Login(second.Username, "Concurrent!2026");
    Execute("UPDATE users SET role='employee' WHERE id<>@first AND id<>@second", ("first", first.Id), ("second", second.Id));
    try
    {
        using var barrier = new Barrier(2);
        var ids = new[] { first.Id, second.Id };
        var tasks = Enumerable.Range(0, 2).Select(index => Task.Run(() =>
        {
            var service = new UserService(new UserRepository(factory), contexts[index], hasher);
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            try
            {
                if (operation == "demote") service.Update(ids[index], "Demoted", UserRole.Employee);
                else if (operation == "block") service.SetActive(ids[1 - index], false);
                else service.Delete(ids[1 - index]);
                return true;
            }
            catch (ValidationException) { return false; }
        })).ToArray();
        var outcomes = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
        Check(outcomes.Count(success => success) == 1, "concurrent " + operation + ": exactly one writer succeeds");
        Check(Convert.ToInt32(Scalar("SELECT count(*) FROM users WHERE role='admin' AND is_active")) == 1,
            "concurrent " + operation + ": last active admin survives");
    }
    finally { Execute("UPDATE users SET role='admin', is_active=true WHERE id=@id", ("id", adminId)); }
    // Remove admin privileges from this pair before the next concurrency scenario.
    Execute("UPDATE users SET role='employee' WHERE id=@first OR id=@second", ("first", first.Id), ("second", second.Id));
}
Console.WriteLine($"All {checks} database checks passed.");
