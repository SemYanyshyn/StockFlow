using System.Data;
using StockFlow.Domain;

namespace StockFlow.DataAccess;

// Integration candidate; compiled only by the isolated prototype test project.
public interface IStockMovementRepository
{
    int Add(StockMovement movement, IDbTransaction transaction);
}
