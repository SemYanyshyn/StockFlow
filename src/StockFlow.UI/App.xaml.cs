using System;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;
using StockFlow.UI.Services;
using StockFlow.UI.ViewModels;
using StockFlow.UI.Views;

namespace StockFlow.UI;

public partial class App : Application
{
    private IConfigurationRoot? _configuration;

    public static IServiceProvider ServiceProvider { get; private set; } = null!;
    public App()
    {
        DispatcherUnhandledException += (sender, args) =>
        {
            MessageBox.Show(
                "Сталася неочікувана помилка. Спробуйте повторити дію.",
                "Помилка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IConfirmationService, ConfirmationService>();
        
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginView>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddTransient<ProductsViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<SuppliersViewModel>();
        services.AddTransient<WarehousesViewModel>();
        services.AddTransient<StockBalancesViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<StockMovementsViewModel>();
        services.AddTransient<UsersViewModel>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _configuration = StockFlowConfiguration.Load();
            var services = new ServiceCollection();
            services.AddStockFlow(_configuration);
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            var login = ServiceProvider.GetRequiredService<LoginView>();
            if (login.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            // Construct the shell and all role-sensitive view models only after authentication.
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.DataContext = ServiceProvider.GetRequiredService<MainWindowViewModel>();
            mainWindow.Show();
        }
        catch (InvalidOperationException error)
        {
            MessageBox.Show(error.Message, "StockFlow", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (ServiceProvider as IDisposable)?.Dispose();
        (_configuration as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}