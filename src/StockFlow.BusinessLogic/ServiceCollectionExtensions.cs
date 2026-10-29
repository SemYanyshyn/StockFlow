using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.DataAccess;

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
        return services;
    }
}
