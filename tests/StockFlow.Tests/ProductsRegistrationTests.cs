using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Products;
using StockFlow.DataAccess.Products;
using Xunit;

namespace StockFlow.Tests;

public sealed class ProductsRegistrationTests
{
    private const string Valid = "Host=127.0.0.1;Port=1;Database=stockflow_test;Username=unit_test;Pooling=false";

    private static ServiceProvider Provider() => new ServiceCollection()
        .AddStockFlow(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:StockFlow"] = Valid })
            .Build())
        .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

    [Fact]
    public void ServicesAndRepositoriesResolveWithoutDatabase()
    {
        using var provider = Provider();
        Assert.IsType<CategoryRepository>(provider.GetRequiredService<ICategoryRepository>());
        Assert.IsType<ProductRepository>(provider.GetRequiredService<IProductRepository>());
        Assert.NotNull(provider.GetRequiredService<CategoryService>());
        Assert.NotNull(provider.GetRequiredService<ProductService>());
    }

    [Fact]
    public void ServicesAreSingletons()
    {
        using var provider = Provider();
        Assert.Same(provider.GetRequiredService<CategoryService>(), provider.GetRequiredService<CategoryService>());
        Assert.Same(provider.GetRequiredService<ProductService>(), provider.GetRequiredService<ProductService>());
    }
}
