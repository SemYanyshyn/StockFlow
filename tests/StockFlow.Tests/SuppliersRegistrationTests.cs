using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Suppliers;
using StockFlow.DataAccess.Suppliers;
using Xunit;

namespace StockFlow.Tests;

public sealed class SuppliersRegistrationTests
{
    private const string Valid = "Host=127.0.0.1;Port=1;Database=stockflow_test;Username=unit_test;Pooling=false";

    private static ServiceProvider CreateProvider() => new ServiceCollection()
        .AddStockFlow(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:StockFlow"] = Valid
            })
            .Build())
        .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

    [Fact]
    public void SupplierRepositoryAndServiceResolveWithoutDatabase()
    {
        using var provider = CreateProvider();
        Assert.IsType<SupplierRepository>(provider.GetRequiredService<ISupplierRepository>());
        Assert.NotNull(provider.GetRequiredService<SupplierService>());
    }

    [Fact]
    public void SupplierServiceAndRepositoryAreSingletons()
    {
        using var provider = CreateProvider();
        Assert.Same(provider.GetRequiredService<SupplierService>(), provider.GetRequiredService<SupplierService>());
        Assert.Same(provider.GetRequiredService<ISupplierRepository>(), provider.GetRequiredService<ISupplierRepository>());
    }
}
