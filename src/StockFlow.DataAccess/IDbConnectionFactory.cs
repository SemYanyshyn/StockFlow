using System.Data;

namespace StockFlow.DataAccess;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}