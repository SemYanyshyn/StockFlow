using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;
using StockFlow.DataAccess;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests.Service;

public sealed class StockMovementDependencyInjectionTests
{
    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:StockFlow"] = "Host=127.0.0.1;Port=1;Database=prototype;Username=test;Pooling=false"
        }).Build();

    [Fact]
    public void AddStockFlowRegistersTheRepositoryRequiredByMovementServices()
    {
        var services = new ServiceCollection();
        services.AddStockFlow(Config()).AddStockMovementServices();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(provider.GetRequiredService<StockMovementService>());
        Assert.IsType<StockRepository>(provider.GetRequiredService<IStockRepository>());
        Assert.IsType<StockMovementRepository>(provider.GetRequiredService<IStockMovementRepository>());
        Assert.IsType<NpgsqlStockTransactionFactory>(provider.GetRequiredService<IStockTransactionFactory>());
    }

    [Fact]
    public void MovementServicesResolveWithAnExplicitStockRepositoryOverride()
    {
        var services = new ServiceCollection();
        services.AddStockFlow(Config()).AddStockMovementServices();
        services.AddSingleton<IStockRepository, UnopenedStockRepository>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(provider.GetRequiredService<StockMovementService>());
        Assert.IsType<UnopenedStockRepository>(provider.GetRequiredService<IStockRepository>());
    }

    private sealed class UnopenedStockRepository : IStockRepository
    {
        public Stock GetForUpdate(int productId, int warehouseId, IDbTransaction transaction) =>
            throw new InvalidOperationException("No database operation is expected while resolving DI");

        public void Save(Stock stock, IDbTransaction transaction) =>
            throw new InvalidOperationException("No database operation is expected while resolving DI");

        public void SetMinThreshold(int productId, int warehouseId, int minThreshold) =>
            throw new InvalidOperationException("No database operation is expected while resolving DI");

        public IReadOnlyList<Stock> GetAll(int? warehouseId = null, IDbTransaction? transaction = null) =>
            Array.Empty<Stock>();
    }
}
