namespace StockFlow.Domain;

// TEST-ONLY fixture for the proposed shared contract. It is not a production Stock
// implementation and must be removed when tests reference Sasha's real Domain.Stock.
public sealed class Stock
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int Quantity { get; set; }
    public int MinThreshold { get; set; }
}
