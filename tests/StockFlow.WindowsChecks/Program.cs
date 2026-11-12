using System.Diagnostics;
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
using StockFlow.UI.Services;
using StockFlow.UI.ViewModels;
using StockFlow.UI.Views;

internal static class Program
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
        var manageButtons = Descendants<Button>(employeeView).Where(b => b.Content?.ToString() is "Редагувати" or "Видалити" or "+ Додати постачальника").ToArray();
        Check(manageButtons.Length > 0 && !employeeSuppliers.CanManage && manageButtons.All(b =>
        {
            for (DependencyObject? current = b; current is not null; current = VisualTreeHelper.GetParent(current))
                if (current is UIElement element && element.Visibility != Visibility.Visible) return true;
            return false;
        }), "employee supplier controls hidden");
        var denied = employeeProvider.GetRequiredService<UsersViewModel>();
        await denied.LoadAsync();
        Check(denied.ErrorMessage == UserService.AdminOnlyMessage && denied.Users.Count == 0, "employee user-screen access denied by service");

        settings.Port = 1; settings.Timeout = 1;
        using var unavailable = Provider(settings.ConnectionString);
        var failedLogin = new LoginViewModel(unavailable.GetRequiredService<AuthService>()) { Username = "admin" };
        await failedLogin.LoginCommand.ExecuteAsync("AdminDemo!2026");
        Check(failedLogin.ErrorMessage is not null && !failedLogin.IsBusy && !unavailable.GetRequiredService<AuthenticatedUserContext>().IsAuthenticated, "connection failure handled by login VM without crash");
        Check(trace.Errors.Count == 0, "WPF XAML binding trace contains no errors");
        Console.WriteLine($"All {_checks} Windows/WPF checks passed. These are programmatic checks, not manual desktop automation.");
    }
}
