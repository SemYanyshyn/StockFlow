using StockFlow.Domain;

namespace StockFlow.DataAccess.Users;

public interface IUserRepository
{
    // Username identity follows the existing case-sensitive UNIQUE(username).
    User? GetByUsername(string username);
}
