using System.Data;
using StockFlow.Domain;

namespace StockFlow.DataAccess
{
    public interface IStockRepository
    {
        Stock GetForUpdate(int productId, int warehouseId, IDbTransaction transaction);
        void Save(Stock stock, IDbTransaction transaction);
        IReadOnlyList<Stock> GetAll(int? warehouseId);
    }
}
