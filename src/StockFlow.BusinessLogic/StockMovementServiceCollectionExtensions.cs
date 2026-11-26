using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace StockFlow.BusinessLogic;

public static class StockMovementServiceCollectionExtensions
{
    /// <summary>Activate after the stock module registers its real IStockRepository.</summary>
    public static IServiceCollection AddStockMovementServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddTransient<StockMovementService>();
        return services;
    }
}
