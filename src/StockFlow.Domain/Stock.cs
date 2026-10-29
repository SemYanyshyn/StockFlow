using System;

namespace StockFlow.Domain
{
    public class Stock
    {
        public int ProductId { get; set; }
        public int WarehouseId { get; set; }
        public int Quantity { get; set; }
        public int MinThreshold { get; set; }

        public bool IsLowStock() => Quantity < MinThreshold;

        public bool Validate() =>
            ProductId > 0 &&
            WarehouseId > 0 &&
            Quantity >= 0 &&
            MinThreshold >= 0;
    }
}
