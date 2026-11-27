using System.Diagnostics;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Suppliers;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess;
using StockFlow.UI.Services;
using StockFlow.UI.ViewModels;
using StockFlow.UI.Views;

internal static partial class Program
{
    private sealed class Confirmation : IConfirmationService
    {
        public bool Answer { get; set; }
        public int Calls { get; private set; }
        public bool Confirm(string message) { Calls++; return Answer; }
    }
    private sealed class BindingErrors : TraceListener
    {
        public List<string> Errors { get; } = new();
        public override void Write(string? message) { if (message is not null) Errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
    private static int _checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("FAILED: " + message);
        _checks++; Console.WriteLine("PASS " + message);
    }
    [STAThread]
    public static int Main()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("pack://application:,,,/StockFlow.UI;component/Styles/Theme.xaml") });
        var exitCode = 1;
        application.Dispatcher.InvokeAsync(async () =>
        {
            try { await RunChecks(); exitCode = 0; }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { application.Shutdown(); }
        });
        application.Run();
        return exitCode;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T result) yield return result;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static async Task Render(FrameworkElement element, string name, double width = 1100, double height = 650)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        await element.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen()) drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
        bitmap.Render(background);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var directory = Path.Combine("TestResults", "windows-checks"); Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(stream);
    }
    private static object? Scalar(string connectionString, string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        return command.ExecuteScalar();
    }
    private static void Execute(string connectionString, string sql, params (string Name, object? Value)[] parameters)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }
    private static ServiceProvider Provider(string connection)
    {
        var services = new ServiceCollection().AddStockFlow(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:StockFlow"] = connection }).Build());
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddTransient<ProductsViewModel>(); services.AddTransient<CategoriesViewModel>();
        services.AddTransient<SuppliersViewModel>(); services.AddTransient<WarehousesViewModel>();
        services.AddTransient<StockMovementsViewModel>(); services.AddTransient<StockBalancesViewModel>();
        services.AddTransient<ReportsViewModel>(); services.AddTransient<UsersViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<IConfirmationService, Confirmation>();
        return services.BuildServiceProvider();
    }
    private static async Task RunChecks()
    {
        var connection = Environment.GetEnvironmentVariable("STOCKFLOW_TEST_CONNECTION") ?? throw new InvalidOperationException("Set STOCKFLOW_TEST_CONNECTION");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host is not ("127.0.0.1" or "localhost") || !settings.Database!.StartsWith("stockflow_isolated_", StringComparison.Ordinal))
            throw new InvalidOperationException("Only loopback stockflow_isolated_* databases are permitted.");
        var trace = new BindingErrors();
        PresentationTraceSources.DataBindingSource.Listeners.Add(trace);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        using var provider = Provider(connection);
        var factory = provider.GetRequiredService<DbConnectionFactory>();

        void AddParameters(IDbCommand command, params (string Name, object Value)[] parameters)
        {
            foreach (var parameter in parameters)
            {
                var dbParameter = command.CreateParameter();
                dbParameter.ParameterName = parameter.Name;
                dbParameter.Value = parameter.Value;
                command.Parameters.Add(dbParameter);
            }
        }

        int ScalarInt(string sql, params (string Name, object Value)[] parameters)
        {
            using var dbConnection = factory.CreateConnection();
            dbConnection.Open();
            using var command = dbConnection.CreateCommand();
            command.CommandText = sql;
            AddParameters(command, parameters);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        void Execute(string sql, params (string Name, object Value)[] parameters)
        {
            using var dbConnection = factory.CreateConnection();
            dbConnection.Open();
            using var command = dbConnection.CreateCommand();
            command.CommandText = sql;
            AddParameters(command, parameters);
            command.ExecuteNonQuery();
        }

        List<(int ProductId, int WarehouseId, int Quantity, int MinThreshold)> ReadStockRows()
        {
            using var dbConnection = factory.CreateConnection();
            dbConnection.Open();
            using var command = dbConnection.CreateCommand();
            command.CommandText = """
                SELECT product_id, warehouse_id, quantity, min_threshold
                FROM stock
                ORDER BY product_id, warehouse_id;
                """;
            using var reader = command.ExecuteReader();
            var rows = new List<(int ProductId, int WarehouseId, int Quantity, int MinThreshold)>();
            while (reader.Read())
                rows.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)));
            return rows;
        }

        void RestoreStockRows(IReadOnlyList<(int ProductId, int WarehouseId, int Quantity, int MinThreshold)> rows)
        {
            using var dbConnection = factory.CreateConnection();
            dbConnection.Open();
            using var transaction = dbConnection.BeginTransaction();
            using (var delete = dbConnection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM stock";
                delete.ExecuteNonQuery();
            }

            foreach (var row in rows)
            {
                using var insert = dbConnection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO stock(product_id, warehouse_id, quantity, min_threshold)
                    VALUES (@product, @warehouse, @quantity, @threshold);
                    """;
                AddParameters(insert,
                    ("product", row.ProductId),
                    ("warehouse", row.WarehouseId),
                    ("quantity", row.Quantity),
                    ("threshold", row.MinThreshold));
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        var auth = provider.GetRequiredService<AuthService>();
        var context = provider.GetRequiredService<ICurrentUserContext>();
        var confirmation = (Confirmation)provider.GetRequiredService<IConfirmationService>();
        var login = new LoginViewModel(auth) { Username = "admin" };
        await login.LoginCommand.ExecuteAsync("wrong");
        Check(login.ErrorMessage == AuthService.InvalidCredentialsMessage && !((AuthenticatedUserContext)context).IsAuthenticated, "login VM invalid credentials");
        var loginView = new LoginView(login);
        await Render((FrameworkElement)loginView.Content, "login", 410, 360);
        loginView.Close();
        var succeeded = false;
        login.LoginSucceeded += () => succeeded = true;
        await login.LoginCommand.ExecuteAsync("AdminDemo!2026");
        Check(succeeded && context.IsAdmin, "admin login VM establishes real context");
        var shell = provider.GetRequiredService<MainWindowViewModel>();
        Check(shell.IsAdmin, "admin shell resolves permissions after login");

        var reportToken = Guid.NewGuid().ToString("N")[..8];
        var reportProductName = "SA7-4 report " + reportToken;
        var reportNumberPrefix = "SA7-4-" + reportToken;
        var reportDateFrom = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Unspecified);
        var reportDateTo = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Unspecified);
        try
        {
            var reportProductId = ScalarInt(
                "INSERT INTO products(name, unit, category_id) VALUES (@name, 'piece', (SELECT min(id) FROM categories)) RETURNING id",
                ("name", reportProductName));
            var reportWarehouseId = ScalarInt("SELECT min(id) FROM warehouses");
            var supplierId = ScalarInt("SELECT min(id) FROM suppliers");
            Execute(
                """
                INSERT INTO stock(product_id, warehouse_id, quantity, min_threshold)
                VALUES (@product, @warehouse, 4, 2);
                """,
                ("product", reportProductId), ("warehouse", reportWarehouseId));
            Execute(
                """
                INSERT INTO stock_movements(
                    number, type, product_id, warehouse_id, supplier_id, user_id, quantity, movement_date)
                VALUES
                    (@before, 'receipt', @product, @warehouse, @supplier, @user, 100, @beforeDate),
                    (@receipt, 'receipt', @product, @warehouse, @supplier, @user, 6, @receiptDate),
                    (@writeOff, 'write_off', @product, @warehouse, NULL, @user, 2, @writeOffDate);
                """,
                ("before", reportNumberPrefix + "-before"),
                ("receipt", reportNumberPrefix + "-receipt"),
                ("writeOff", reportNumberPrefix + "-write-off"),
                ("product", reportProductId),
                ("warehouse", reportWarehouseId),
                ("supplier", supplierId),
                ("user", context.UserId),
                ("beforeDate", reportDateFrom.AddDays(-1)),
                ("receiptDate", reportDateFrom),
                ("writeOffDate", reportDateTo.AddHours(23).AddMinutes(59)));

            var reports = provider.GetRequiredService<ReportsViewModel>();
            reports.DateFrom = reportDateFrom;
            reports.DateTo = reportDateTo;
            reports.SelectedReportType = reports.ReportTypes.Single(type => type == "Рух товару за період");
            Check(reports.IsMovementReport && reports.IsPeriodVisible, "екран звітів перемикає тип на рух товару за період");
            var reportRow = reports.MovementRows.Single(row => row.ProductName == reportProductName);
            Check(reportRow.IncomingDisplay == "+6 шт." &&
                  reportRow.OutgoingDisplay == "-2 шт." &&
                  reportRow.BalanceDisplay == "4 шт.",
                "звіт показує точні суми рухів у межах періоду");

            var reportsView = new ReportsView { DataContext = reports };
            await Render(reportsView, "reports-with-data");
            var movementGrid = Descendants<DataGrid>(reportsView).Single(grid => grid.Visibility == Visibility.Visible);
            Check(movementGrid.Items.Count > 0, "таблиця звіту показує результати за період");

            const string invalidPeriodMessage = "Дата початку не може бути пізнішою за кінцеву дату.";
            reports.DateFrom = reportDateTo.AddDays(1);
            reports.DateTo = reportDateTo;
            reports.GenerateReportCommand.Execute(null);
            await Render(reportsView, "reports-invalid-period");
            var reportErrorText = Descendants<TextBlock>(reportsView)
                .SingleOrDefault(text => text.Text == invalidPeriodMessage);
            Check(reports.ErrorMessage == invalidPeriodMessage &&
                  reportErrorText is { Visibility: Visibility.Visible },
                "екран звітів показує українське повідомлення про некоректний період");

            reports.DateFrom = new DateTime(1900, 1, 1);
            reports.DateTo = new DateTime(1900, 1, 2);
            reports.GenerateReportCommand.Execute(null);
            await Render(reportsView, "reports-empty-period");
            movementGrid = Descendants<DataGrid>(reportsView).Single(grid => grid.Visibility == Visibility.Visible);
            Check(reports.ErrorMessage.Length == 0 &&
                  reports.MovementRows.Count == 0 &&
                  movementGrid.Items.Count == 0,
                "екран звітів показує порожній стан для періоду без рухів");
        }
        finally
        {
            Execute(
                "DELETE FROM stock_movements WHERE product_id IN (SELECT id FROM products WHERE name = @name)",
                ("name", reportProductName));
            Execute(
                "DELETE FROM stock WHERE product_id IN (SELECT id FROM products WHERE name = @name)",
                ("name", reportProductName));
            Execute("DELETE FROM products WHERE name = @name", ("name", reportProductName));
        }

        var balanceToken = Guid.NewGuid().ToString("N")[..8];
        var lowProductName = "SA7-4 low " + balanceToken;
        var normalProductName = "SA7-4 normal " + balanceToken;
        try
        {
            var categoryId = ScalarInt("SELECT min(id) FROM categories");
            var warehouseId = ScalarInt("SELECT min(id) FROM warehouses");
            var lowProductId = ScalarInt(
                "INSERT INTO products(name, unit, category_id) VALUES (@name, 'piece', @category) RETURNING id",
                ("name", lowProductName), ("category", categoryId));
            var normalProductId = ScalarInt(
                "INSERT INTO products(name, unit, category_id) VALUES (@name, 'piece', @category) RETURNING id",
                ("name", normalProductName), ("category", categoryId));
            Execute(
                """
                INSERT INTO stock(product_id, warehouse_id, quantity, min_threshold)
                VALUES (@lowProduct, @warehouse, 2, 5), (@normalProduct, @warehouse, 8, 5);
                """,
                ("lowProduct", lowProductId),
                ("normalProduct", normalProductId),
                ("warehouse", warehouseId));

            var balances = provider.GetRequiredService<StockBalancesViewModel>();
            var lowBalance = balances.FilteredRows.Single(row => row.ProductName == lowProductName);
            var normalBalance = balances.FilteredRows.Single(row => row.ProductName == normalProductName);
            Check(lowBalance.IsLow && !normalBalance.IsLow, "екран залишків визначає критичний рядок");

            var balancesView = new StockBalancesView { DataContext = balances };
            await Render(balancesView, "stock-balances-critical");
            var rows = Descendants<DataGridRow>(balancesView).ToArray();
            var lowRow = rows.SingleOrDefault(row =>
                row.DataContext is StockBalanceRowItem item && item.ProductName == lowProductName);
            var normalRow = rows.SingleOrDefault(row =>
                row.DataContext is StockBalanceRowItem item && item.ProductName == normalProductName);
            Check(lowRow is not null && normalRow is not null, "таблиця залишків створює візуальні рядки перевірки");
            Check(!Equals(lowRow!.Background, normalRow!.Background),
                "рядок нижче мінімальної норми має інше тло");
        }
        finally
        {
            Execute(
                """
                DELETE FROM stock
                WHERE product_id IN (SELECT id FROM products WHERE name IN (@lowName, @normalName));
                """,
                ("lowName", lowProductName), ("normalName", normalProductName));
            Execute(
                "DELETE FROM products WHERE name IN (@lowName, @normalName)",
                ("lowName", lowProductName), ("normalName", normalProductName));
        }

        var originalStockRows = ReadStockRows();
        try
        {
            Execute("DELETE FROM stock");
            var emptyBalances = provider.GetRequiredService<StockBalancesViewModel>();
            Check(emptyBalances.FilteredRows.Count == 0, "модель залишків має порожній список для порожньої таблиці stock");
            var emptyBalancesView = new StockBalancesView { DataContext = emptyBalances };
            await Render(emptyBalancesView, "stock-balances-empty");
            var emptyBalancesGrid = Descendants<DataGrid>(emptyBalancesView).Single();
            Check(emptyBalancesGrid.Items.Count == 0, "екран залишків показує порожній стан таблиці");
        }
        finally
        {
            RestoreStockRows(originalStockRows);
        }
        Check(ReadStockRows().SequenceEqual(originalStockRows), "початкові записи stock відновлено після перевірки порожнього стану");

        var suppliers = provider.GetRequiredService<SuppliersViewModel>();
        await suppliers.LoadAsync();
        var first = suppliers.Suppliers[0];
        suppliers.SearchText = first.Name;
        suppliers.EditCommand.Execute(first);
        Check(suppliers.NewName == first.Name && suppliers.NewPhone == (first.Phone ?? ""), "supplier editor prefill");
        suppliers.NewName = "Cancelled";
        suppliers.CancelEditorCommand.Execute(null);
        Check(first.Name != "Cancelled" && !suppliers.IsEditorOpen, "supplier cancellation leaves original unchanged");
        suppliers.EditCommand.Execute(first);
        suppliers.NewName = first.Name + " UI";
        suppliers.NewPhone = " "; suppliers.NewEmail = " ";
        await suppliers.AddCommand.ExecuteAsync(null);
        Check(suppliers.SearchText == first.Name && suppliers.Suppliers.Any(s => s.Id == first.Id && s.Phone is null && s.Email is null), "supplier save refreshes active search and optional contacts");
        var supplierView = new SuppliersView { DataContext = suppliers };
        await Render(supplierView, "suppliers-admin", 760, 560);
        confirmation.Answer = false;
        await suppliers.DeleteCommand.ExecuteAsync(suppliers.Suppliers[0]);
        Check(confirmation.Calls == 1 && suppliers.Suppliers.Count > 0, "supplier deletion cancellation through confirmation service");
        suppliers.SearchText = ""; suppliers.OpenEditorCommand.Execute(null); suppliers.NewName = "Delete UI";
        await suppliers.AddCommand.ExecuteAsync(null);
        confirmation.Answer = true;
        var disposableSupplier = suppliers.Suppliers.Single(s => s.Name == "Delete UI");
        await suppliers.DeleteCommand.ExecuteAsync(disposableSupplier);
        Check(!suppliers.Suppliers.Any(s => s.Id == disposableSupplier.Id), "supplier confirmed delete refreshes list");

        var users = provider.GetRequiredService<UsersViewModel>();
        await users.LoadAsync();
        Check(users.Users.Count == 2 && users.CanManage, "users load on admin screen");
        users.CreateCommand.Execute(null); users.EditorUsername = "ui_check"; users.EditorFullName = "Іван Перевірка";
        await users.SaveCommand.ExecuteAsync("UiCheck!2026");
        Check(users.Users.Any(u => u.Username == "ui_check") && !users.IsEditorOpen, "user creation command and list refresh");
        users.SearchText = "ІВАН";
        Check(users.Users.Count == 1, "user Cyrillic case-insensitive search");
        var row = users.Users[0]; users.EditCommand.Execute(row);
        Check(users.EditorUsername == row.Username && users.EditorFullName == row.FullName && users.IsEditing, "user editor prefill and immutable login mode");
        users.EditorFullName = "Changed"; users.CancelCommand.Execute(null);
        Check(users.Users[0].FullName == "Іван Перевірка", "user cancellation preserves record");
        users.EditCommand.Execute(row); users.EditorFullName = "Іван Оновлений"; users.EditorRole = users.Roles[1];
        await users.SaveCommand.ExecuteAsync(null);
        Check(users.Users.Single().FullName == "Іван Оновлений" && users.Users.Single().User.Role == StockFlow.Domain.UserRole.Admin, "user edit command saves name and role");
        users.FilterRole = users.FilterRoles[1]; Check(users.IsEmpty, "role filter displays empty-result state");
        users.FilterRole = users.FilterRoles[0];
        await users.ToggleCommand.ExecuteAsync(users.Users.Single());
        Check(!users.Users.Single().User.IsActive, "user block updates status");
        await users.ToggleCommand.ExecuteAsync(users.Users.Single());
        Check(users.Users.Single().User.IsActive, "user unblock updates status");
        users.SearchText = "";
        await users.ToggleCommand.ExecuteAsync(users.Users.Single(u => u.Username == "admin"));
        Check(users.ErrorMessage == UserService.SelfBlockMessage, "user UI shows self-block refusal");
        users.EditCommand.Execute(users.Users.Single(u => u.Username == "ui_check"));
        var userView = new UsersView { DataContext = users };
        await Render(userView, "users-editor", 760, 560);
        var usernameBox = Descendants<TextBox>(userView).Single(t => System.Windows.Automation.AutomationProperties.GetName(t) == "Логін користувача");
        Check(usernameBox.IsReadOnly && usernameBox.Text == "ui_check", "WPF login field is read-only during edit");
        users.CancelCommand.Execute(null);
        confirmation.Answer = false;
        await users.DeleteCommand.ExecuteAsync(users.Users.Single(u => u.Username == "ui_check"));
        Check(users.Users.Count == 3, "user delete cancellation");
        confirmation.Answer = true;
        await users.DeleteCommand.ExecuteAsync(users.Users.Single(u => u.Username == "ui_check"));
        Check(users.Users.Count == 2, "user confirmed deletion");
        await Render(userView, "users-list", 760, 560);

        var accounts = provider.GetRequiredService<UserService>();
        var replacement = accounts.Create("replacement_ui", "Replacement", "Replacement!2026", StockFlow.Domain.UserRole.Admin);
        var originalAdminId = context.UserId;
        users.EditCommand.Execute(users.Users.Single(u => u.Username == "admin"));
        users.EditorRole = users.Roles[0];
        await users.SaveCommand.ExecuteAsync(null);
        Check(!context.IsAdmin && !shell.IsAdmin && !users.CanManage, "self-demotion refreshes context, shell menu and CanManage");
        auth.Login("replacement_ui", "Replacement!2026");
        accounts.Update(originalAdminId, "Адміністратор", StockFlow.Domain.UserRole.Admin);
        auth.Login("admin", "AdminDemo!2026");
        accounts.Delete(replacement.Id);

        using var employeeProvider = Provider(connection);
        var employeeLogin = new LoginViewModel(employeeProvider.GetRequiredService<AuthService>()) { Username = "employee" };
        await employeeLogin.LoginCommand.ExecuteAsync("EmployeeDemo!2026");
        Check(employeeLogin.ErrorMessage is null && !employeeProvider.GetRequiredService<MainWindowViewModel>().IsAdmin, "employee login and shell permissions");
        var employeeSuppliers = employeeProvider.GetRequiredService<SuppliersViewModel>();
        await employeeSuppliers.LoadAsync();
        var employeeView = new SuppliersView { DataContext = employeeSuppliers };
        await Render(employeeView, "suppliers-employee");
        var manageButtons = Descendants<Button>(employeeView).Where(b => b.Content?.ToString() is "Редагувати" or "Видалити" or "+ Додати").ToArray();
        Check(manageButtons.Length > 0 && !employeeSuppliers.CanManage && manageButtons.All(b =>
        {
            for (DependencyObject? current = b; current is not null; current = VisualTreeHelper.GetParent(current))
                if (current is UIElement element && element.Visibility != Visibility.Visible) return true;
            return false;
        }), "employee supplier controls hidden");

        var movements = employeeProvider.GetRequiredService<StockMovementsViewModel>();
        await movements.LoadAsync();
        var movementsView = new StockMovementsView { DataContext = movements };
        await Render(movementsView, "movements-list");
        Check(Descendants<Button>(movementsView).Any(button => button.Content?.ToString() == "+ Створити накладну"),
            "employee movement screen renders the create action");
        await movements.OpenCreateCommand.ExecuteAsync(null);
        Check(movements.CreateProducts.Count > 0 && movements.CreateWarehouses.Count > 0 && movements.CreateSuppliers.Count > 0,
            "movement form loads products, warehouses and suppliers");
        Check(movements.CreateTypes.Select(option => option.Name).SequenceEqual(new[] { "Надходження", "Списання" }),
            "movement form offers the domain-supported operation types");

        var selectedProduct = movements.SelectedCreateProduct
            ?? throw new InvalidOperationException("No product is available for the movement UI check.");
        var selectedWarehouse = movements.SelectedCreateWarehouse
            ?? throw new InvalidOperationException("No warehouse is available for the movement UI check.");
        var movementNumber = movements.CreationNumber;
        var hadStock = Convert.ToBoolean(Scalar(connection,
            "SELECT EXISTS(SELECT 1 FROM stock WHERE product_id=@product AND warehouse_id=@warehouse)",
            ("product", selectedProduct.Id), ("warehouse", selectedWarehouse.Id)));
        var oldQuantity = hadStock
            ? Convert.ToInt32(Scalar(connection,
                "SELECT quantity FROM stock WHERE product_id=@product AND warehouse_id=@warehouse",
                ("product", selectedProduct.Id), ("warehouse", selectedWarehouse.Id)))
            : 0;
        var oldThreshold = hadStock
            ? Convert.ToInt32(Scalar(connection,
                "SELECT min_threshold FROM stock WHERE product_id=@product AND warehouse_id=@warehouse",
                ("product", selectedProduct.Id), ("warehouse", selectedWarehouse.Id)))
            : 0;
        try
        {
            movements.CreationQuantity = "0";
            await Render(movementsView, "movements-create");
            var quantityBox = Descendants<TextBox>(movementsView)
                .Single(box => System.Windows.Automation.AutomationProperties.GetName(box) == "Кількість товару для руху");
            Check(quantityBox.Text == "0", "movement form displays the selected quantity input");
            await movements.SaveMovementCommand.ExecuteAsync(null);
            Check(movements.CreationError == "Кількість має бути додатним цілим числом.",
                "movement UI reports invalid quantity in Ukrainian");
            Check(Descendants<TextBlock>(movementsView)
                .Any(text => text.Text == "Кількість має бути додатним цілим числом."),
                "movement UI displays the validation message");
            Check(!Convert.ToBoolean(Scalar(connection, "SELECT EXISTS(SELECT 1 FROM stock_movements WHERE number=@number)",
                ("number", movementNumber))), "invalid movement data is not persisted");

            movements.CreationQuantity = "1";
            await movements.SaveMovementCommand.ExecuteAsync(null);
            Check(movements.CreationError is null && movements.Movements.Any(item => item.Number == movementNumber),
                "movement UI creates a receipt and refreshes the invoice list");
            Check(Convert.ToBoolean(Scalar(connection, "SELECT EXISTS(SELECT 1 FROM stock_movements WHERE number=@number)",
                ("number", movementNumber))), "movement UI persists the created invoice");
        }
        finally
        {
            Execute(connection, "DELETE FROM stock_movements WHERE number=@number", ("number", movementNumber));
            if (hadStock)
            {
                Execute(connection, """
                    INSERT INTO stock(product_id, warehouse_id, quantity, min_threshold)
                    VALUES (@product, @warehouse, @quantity, @threshold)
                    ON CONFLICT (product_id, warehouse_id)
                    DO UPDATE SET quantity=EXCLUDED.quantity, min_threshold=EXCLUDED.min_threshold
                    """, ("product", selectedProduct.Id), ("warehouse", selectedWarehouse.Id),
                    ("quantity", oldQuantity), ("threshold", oldThreshold));
            }
            else
            {
                Execute(connection, "DELETE FROM stock WHERE product_id=@product AND warehouse_id=@warehouse",
                    ("product", selectedProduct.Id), ("warehouse", selectedWarehouse.Id));
            }
        }

        var denied = employeeProvider.GetRequiredService<UsersViewModel>();
        await denied.LoadAsync();
        Check(denied.ErrorMessage == UserService.AdminOnlyMessage && denied.Users.Count == 0, "employee user-screen access denied by service");
        await CheckInvoices(provider, employeeProvider);
        var employeeBalances = employeeProvider.GetRequiredService<StockBalancesViewModel>();
        var employeeBalancesView = new StockBalancesView { DataContext = employeeBalances };
        await Render(employeeBalancesView, "stock-balances-employee");
        var employeeThresholdEditor = Descendants<StackPanel>(employeeBalancesView)
            .Single(panel => panel.Name == "ThresholdEditorPanel");
        Check(!employeeBalances.CanEditThreshold && employeeThresholdEditor.Visibility == Visibility.Collapsed,
            "employee cannot see the minimum threshold editor");

        var stockRepo = provider.GetRequiredService<StockFlow.DataAccess.IStockRepository>();
        var productRepo = provider.GetRequiredService<StockFlow.DataAccess.Products.IProductRepository>();
        var warehouseRepo = provider.GetRequiredService<StockFlow.DataAccess.Warehouses.IWarehouseRepository>();
        var existingStock = stockRepo.GetAll().ToList();

        try
        {
            using (var clearConn = new NpgsqlConnection(connection))
            {
                clearConn.Open();
                using var clearCmd = new NpgsqlCommand("DELETE FROM stock;", clearConn);
                clearCmd.ExecuteNonQuery();
            }

            var emptyStockBalances = provider.GetRequiredService<StockBalancesViewModel>();
            var emptyBalancesView = new StockBalancesView { DataContext = emptyStockBalances };
            await Render(emptyBalancesView, "stock-balances-empty");
            var emptyStateText = Descendants<TextBlock>(emptyBalancesView).Single(t => t.Name == "EmptyStateText");
            Check(emptyStockBalances.FilteredRows.Count == 0 && emptyStockBalances.IsEmpty && emptyStateText.Visibility == Visibility.Visible,
                "stock balances empty database sets IsEmpty and shows empty state");

            var allProducts = productRepo.GetAll();
            var allWarehouses = warehouseRepo.GetAll();
            if (allProducts.Count >= 2 && allWarehouses.Count >= 2)
            {
                using (var seedConn = new NpgsqlConnection(connection))
                {
                    seedConn.Open();
                    using var seedTx = seedConn.BeginTransaction();
                    stockRepo.Save(new StockFlow.Domain.Stock { ProductId = allProducts[0].Id, WarehouseId = allWarehouses[0].Id, Quantity = 2, MinThreshold = 10 }, seedTx);
                    stockRepo.Save(new StockFlow.Domain.Stock { ProductId = allProducts[1].Id, WarehouseId = allWarehouses[1].Id, Quantity = 25, MinThreshold = 10 }, seedTx);
                    seedTx.Commit();
                }

                var stockBalances = provider.GetRequiredService<StockBalancesViewModel>();
                var balancesView = new StockBalancesView { DataContext = stockBalances };
                await Render(balancesView, "stock-balances-list");

                Check(stockBalances.SelectedWarehouse?.Name == "Усі склади" && stockBalances.FilteredRows.Count == 2 && !stockBalances.IsEmpty,
                    "stock balances default filter 'Усі склади' displays all rows");

                var gridRows = Descendants<DataGridRow>(balancesView).ToList();
                var lowRow = gridRows.Single(r => ((StockBalanceRowItem)r.Item).IsLow);
                var normalRow = gridRows.Single(r => !((StockBalanceRowItem)r.Item).IsLow);
                Check(lowRow.Background is SolidColorBrush lowBrush &&
                      lowBrush.Color.A > 0 &&
                      !Equals(lowRow.Background, normalRow.Background),
                    "stock balances highlights rows below minimum threshold");

                var lowBalance = (StockBalanceRowItem)lowRow.Item;
                stockBalances.SelectedRow = lowBalance;
                var thresholdEditor = Descendants<StackPanel>(balancesView)
                    .Single(panel => panel.Name == "ThresholdEditorPanel");
                var thresholdInput = Descendants<TextBox>(balancesView)
                    .Single(textBox => textBox.Name == "MinThresholdInput");
                Check(stockBalances.CanEditThreshold && thresholdEditor.Visibility == Visibility.Visible,
                    "administrator sees the minimum threshold editor");

                const string thresholdInputError = "Введіть ціле числове значення мінімальної норми.";
                thresholdInput.Text = "не число";
                stockBalances.SaveMinThresholdCommand.Execute(null);
                await Render(balancesView, "stock-balances-invalid-threshold");
                var thresholdErrorText = Descendants<TextBlock>(balancesView)
                    .Single(textBlock => textBlock.Name == "ErrorMessageText");
                Check(stockBalances.ErrorMessage == thresholdInputError &&
                      thresholdErrorText.Text == thresholdInputError &&
                      thresholdErrorText.Visibility == Visibility.Visible,
                    "нечислове значення порога показує українську помилку без падіння");

                thresholdInput.Text = string.Empty;
                stockBalances.SaveMinThresholdCommand.Execute(null);
                Check(stockBalances.ErrorMessage == thresholdInputError,
                    "порожнє значення порога відхиляється з повідомленням");

                thresholdInput.Text = "2";
                stockBalances.SaveMinThresholdCommand.Execute(null);
                await Render(balancesView, "stock-balances-updated-threshold");
                var updatedLowBalance = stockBalances.FilteredRows.Single(row =>
                    row.ProductId == lowBalance.ProductId && row.WarehouseId == lowBalance.WarehouseId);
                Check(updatedLowBalance.Quantity == 2 &&
                      updatedLowBalance.MinThreshold == 2 &&
                      !updatedLowBalance.IsLow &&
                      updatedLowBalance.StatusText == "• В нормі" &&
                      stockBalances.ErrorMessage.Length == 0,
                    "збереження порога негайно оновлює статус і зберігає кількість");

                var updatedGridRows = Descendants<DataGridRow>(balancesView).ToList();
                var updatedLowRow = updatedGridRows.Single(row =>
                    ((StockBalanceRowItem)row.Item).ProductId == lowBalance.ProductId &&
                    ((StockBalanceRowItem)row.Item).WarehouseId == lowBalance.WarehouseId);
                var unchangedNormalRow = updatedGridRows.Single(row =>
                    ((StockBalanceRowItem)row.Item).ProductId == ((StockBalanceRowItem)normalRow.Item).ProductId &&
                    ((StockBalanceRowItem)row.Item).WarehouseId == ((StockBalanceRowItem)normalRow.Item).WarehouseId);
                Check(Equals(updatedLowRow.Background, unchangedNormalRow.Background),
                    "підсвічування критичного рядка знімається після досягнення порога");

                stockBalances.SelectedWarehouse = stockBalances.Warehouses.First(w => w.Id == allWarehouses[1].Id);
                Check(stockBalances.FilteredRows.Count == 1 && stockBalances.FilteredRows[0].WarehouseName == allWarehouses[1].Name,
                    "stock balances filters by specific warehouse");

                stockBalances.SelectedWarehouse = stockBalances.Warehouses.First(w => w.Id == 0);
                Check(stockBalances.FilteredRows.Count == 2,
                    "stock balances restores all rows when 'Усі склади' is selected again");
            }
        }
        finally
        {
            using var restoreConn = new NpgsqlConnection(connection);
            restoreConn.Open();
            using var restoreTx = restoreConn.BeginTransaction();
            using (var delCmd = new NpgsqlCommand("DELETE FROM stock;", restoreConn, restoreTx))
            {
                delCmd.ExecuteNonQuery();
            }
            foreach (var item in existingStock)
            {
                stockRepo.Save(item, restoreTx);
            }
            restoreTx.Commit();
        }

        settings.Port = 1; settings.Timeout = 1;
        using var unavailable = Provider(settings.ConnectionString);
        var failedLogin = new LoginViewModel(unavailable.GetRequiredService<AuthService>()) { Username = "admin" };
        await failedLogin.LoginCommand.ExecuteAsync("AdminDemo!2026");
        var failedBalances = unavailable.GetRequiredService<StockBalancesViewModel>();
        var failedBalancesView = new StockBalancesView { DataContext = failedBalances };
        await Render(failedBalancesView, "stock-balances-error");
        var errorText = Descendants<TextBlock>(failedBalancesView).Single(t => t.Name == "ErrorMessageText");
        Check(!string.IsNullOrWhiteSpace(failedBalances.ErrorMessage) &&
              failedBalances.IsEmpty &&
              errorText.Visibility == Visibility.Visible,
            "stock balances displays load error without crashing");
        Check(failedLogin.ErrorMessage is not null && !failedLogin.IsBusy && !unavailable.GetRequiredService<AuthenticatedUserContext>().IsAuthenticated, "connection failure handled by login VM without crash");
        Check(trace.Errors.Count == 0, "WPF XAML binding trace contains no errors");
        Console.WriteLine($"All {_checks} Windows/WPF checks passed. These are programmatic checks, not manual desktop automation.");
    }
}
