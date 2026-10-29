using Microsoft.Extensions.DependencyInjection;

namespace StockFlow.BusinessLogic;

public static class StockMovementServiceCollectionExtensions
{
    /// <summary>Activate after the stock module registers its real IStockRepository.</summary>
    public static IServiceCollection AddStockMovementServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<StockMovementService>();
        return services;
    }
}
