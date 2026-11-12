using StockFlow.Domain;

namespace StockFlow.DataAccess.Users;

// All account writes share one database lock, acquired before any guard reads.
// BusinessLogic owns Commit/Dispose; Dispose without Commit rolls back.
public interface IUserManagementTransaction : IDisposable
{
    IReadOnlyList<User> GetAll();
    User? GetById(int id);
    int CountActiveAdmins();
    bool HasMovements(int id);
    int Add(User user);
    bool Update(User user);
    bool SetActive(int id, bool isActive);
    bool Delete(int id);
    void Commit();
}
