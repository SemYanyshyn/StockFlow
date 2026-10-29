using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess;
using StockFlow.DataAccess.Products;

namespace StockFlow.BusinessLogic;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStockFlow(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Lazy singleton stores configuration only. No shared connection or database access at startup.
        services.AddSingleton<DbConnectionFactory>(_ => new DbConnectionFactory(configuration));
        services.AddSingleton<IStockMovementRepository, StockMovementRepository>();
        services.AddSingleton<IStockTransactionFactory, NpgsqlStockTransactionFactory>();
        // Activate AddStockMovementServices after the stock module registers its real IStockRepository.
        // Module owners add their real services/repositories here when implemented.
        services.AddSingleton<ICurrentUserContext>(_ => TemporaryUserContext.FromConfiguration(configuration));
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<CategoryService>();
        services.AddSingleton<ProductService>();
        return services;
    }
}
