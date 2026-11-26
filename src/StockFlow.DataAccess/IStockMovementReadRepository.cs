using StockFlow.Domain;

namespace StockFlow.DataAccess;

public interface IStockMovementReadRepository
{
    StockMovementList Search(StockMovementFilter filter);
    StockMovementDetails? GetById(int id);
}
