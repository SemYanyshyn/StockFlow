using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;

namespace StockFlow.UI;

public partial class App : Application
{
    private ServiceProvider? _services;
    private IConfigurationRoot? _configuration;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _configuration = StockFlowConfiguration.Load();
            var services = new ServiceCollection();
            services.AddStockFlow(_configuration);
            services.AddSingleton<MainWindow>();
            _services = services.BuildServiceProvider();
            _services.GetRequiredService<MainWindow>().Show();
        }
        catch (InvalidOperationException error)
        {
            MessageBox.Show(error.Message, "StockFlow", MessageBoxButton.OK, MessageBoxImage.Error);
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
