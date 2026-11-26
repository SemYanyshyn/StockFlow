using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Warehouses;
using StockFlow.DataAccess.Warehouses;
using Xunit;

namespace StockFlow.Tests;

public sealed class WarehousesRegistrationTests
{
    private const string Valid = "Host=127.0.0.1;Port=1;Database=stockflow_test;Username=unit_test;Pooling=false";

    private static ServiceProvider CreateProvider() => new ServiceCollection()
        .AddStockFlow(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:StockFlow"] = Valid })
            .Build())
        .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

    [Fact]
    public void WarehouseRepositoryAndServiceResolveWithoutDatabase()
    {
        using var provider = CreateProvider();
        Assert.IsType<WarehouseRepository>(provider.GetRequiredService<IWarehouseRepository>());
        Assert.NotNull(provider.GetRequiredService<WarehouseService>());
    }

    [Fact]
    public void WarehouseServiceIsSingleton()
    {
        using var provider = CreateProvider();
        Assert.Same(provider.GetRequiredService<WarehouseService>(), provider.GetRequiredService<WarehouseService>());
    }
}
