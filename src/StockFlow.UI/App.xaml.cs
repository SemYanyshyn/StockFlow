using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.UI.Services;
using StockFlow.UI.ViewModels;
using StockFlow.UI.Views;

namespace StockFlow.UI;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
    }

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

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = ServiceProvider.GetRequiredService<MainWindowViewModel>();
        mainWindow.Show();
    }
}