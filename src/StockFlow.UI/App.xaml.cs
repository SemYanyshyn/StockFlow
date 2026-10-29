using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using StockFlow.BusinessLogic;
using StockFlow.UI.Services;
using StockFlow.UI.ViewModels;
using StockFlow.UI.Views;

namespace StockFlow.UI;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private ServiceProvider? _services;
    private IConfigurationRoot? _configuration;

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

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
            _services = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            ServiceProvider = _services;
            var mainWindow = _services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _services.GetRequiredService<MainWindowViewModel>();
            mainWindow.Show();
        }
        catch (InvalidOperationException error)
        {
            MessageBox.Show(error.Message, "StockFlow", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
        catch (Exception)
        {
            MessageBox.Show("Не вдалося запустити застосунок. Перевірте налаштування та спробуйте ще раз",
                "StockFlow", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        (_configuration as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}
