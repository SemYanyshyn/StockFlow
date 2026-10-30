using Microsoft.Extensions.DependencyInjection;
using StockFlow.DataAccess.Warehouses;
using StockFlow.BusinessLogic.Warehouses;

namespace StockFlow.BusinessLogic;

public static class DependencyInjection
{
    public static IServiceCollection AddStockFlow(this IServiceCollection services)
    {
        services.AddSingleton<IWarehouseRepository, WarehouseRepository>();
        services.AddSingleton<WarehouseService>();
        return services;
    }
}
