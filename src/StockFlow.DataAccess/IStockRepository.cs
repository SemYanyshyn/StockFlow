using System.Collections.Generic;
using System.Data;
using StockFlow.Domain;

namespace StockFlow.DataAccess;

public interface IStockRepository
{
    Stock GetForUpdate(int productId, int warehouseId, IDbTransaction transaction);
    void Save(Stock stock, IDbTransaction transaction);
    void SetMinThreshold(int productId, int warehouseId, int minThreshold);
    IReadOnlyList<Stock> GetAll(int? warehouseId = null, IDbTransaction? transaction = null);
}
