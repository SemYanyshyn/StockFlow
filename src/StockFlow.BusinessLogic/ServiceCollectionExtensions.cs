using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Suppliers;
using StockFlow.BusinessLogic.Users;
using StockFlow.BusinessLogic.Warehouses;
using StockFlow.DataAccess;
using StockFlow.DataAccess.Products;
using StockFlow.DataAccess.Suppliers;
using StockFlow.DataAccess.Warehouses;
using StockFlow.DataAccess.Users;

namespace StockFlow.BusinessLogic;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStockFlow(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Lazy singleton stores configuration only. No shared connection or database access at startup.
        services.AddSingleton<DbConnectionFactory>(_ => new DbConnectionFactory(configuration));
        // Module owners add their real services/repositories here when implemented.
        services.AddSingleton<AuthenticatedUserContext>();
        services.AddSingleton<ICurrentUserContext>(sp => sp.GetRequiredService<AuthenticatedUserContext>());
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        // Resolve the repository inside Login, so missing/invalid DB settings are shown in the login window.
        services.AddSingleton<AuthService>(sp => new AuthService(
            () => sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<IPasswordHasher>(),
            sp.GetRequiredService<AuthenticatedUserContext>()));
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<CategoryService>();
        services.AddSingleton<ProductService>();
        services.AddSingleton<ISupplierRepository, SupplierRepository>();
        services.AddSingleton<SupplierService>();
        services.AddSingleton<IWarehouseRepository, WarehouseRepository>();
        services.AddSingleton<WarehouseService>();
        return services;
    }
}
