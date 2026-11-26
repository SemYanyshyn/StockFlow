using System.Data;

namespace StockFlow.DataAccess;

public interface IStockTransactionFactory
{
    IStockTransaction Begin();
}

public interface IStockTransaction : IDisposable
{
    IDbTransaction Transaction { get; }
    bool IsActive { get; }
    void Commit();
    void Rollback();
}
