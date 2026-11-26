using System.Data;
using StockFlow.Domain;

namespace StockFlow.DataAccess;

// The caller owns the transaction and its connection.
public interface IStockMovementRepository
{
    int Add(StockMovement movement, IDbTransaction transaction);
}
