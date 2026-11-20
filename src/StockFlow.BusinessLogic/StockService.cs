using StockFlow.BusinessLogic.Products;
using StockFlow.DataAccess;
using StockFlow.DataAccess.Products;
using StockFlow.DataAccess.Warehouses;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic;

public sealed class StockService
{
    private readonly IStockRepository _stockRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;

    public StockService(
        IStockRepository stockRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository)
    {
        _stockRepository = stockRepository ?? throw new ArgumentNullException(nameof(stockRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
    }

    public bool IsBelowThreshold(int quantity, int minThreshold) =>
        minThreshold > 0 && quantity < minThreshold;

    public IReadOnlyList<StockBalanceDto> GetRows(int? warehouseId = null)
    {
        if (warehouseId is <= 0)
        {
            throw new ValidationException("Некоректний ідентифікатор складу.");
        }

        var stocks = _stockRepository.GetAll(warehouseId);
        if (stocks.Count == 0)
        {
            return Array.Empty<StockBalanceDto>();
        }

        var products = _productRepository








.GetAll().ToDictionary(p => p.Id);
        var warehouses = _warehouseRepository.GetAll().ToDictionary(w => w.Id);

        return stocks
            .Select(s =>
            {
                var hasProduct = products.TryGetValue(s.ProductId, out var product);
                var hasWarehouse = warehouses.TryGetValue(s.WarehouseId, out var warehouse);










                return new StockBalanceDto(
                     ProductName: hasProduct ? product!.Name : $"Товар #{s.ProductId}",
                     Unit: hasProduct ? MapUnit(product!.Unit) : "piece",
                     WarehouseName: hasWarehouse ? warehouse!.Name : $"Склад #{s.WarehouseId}",
                     Quantity: s.Quantity,
                     MinThreshold: s.MinThreshold,






                    IsLow: IsBelowThreshold(s.Quantity, s.MinThreshold));
            })
            .OrderBy(r => r.ProductName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.WarehouseName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }







    private static string MapUnit(UnitOfMeasure unit) => unit switch
    {
        UnitOfMeasure.Kilogram => "kg",
        UnitOfMeasure.Liter => "liter",





        _ => "piece"
    };
}
