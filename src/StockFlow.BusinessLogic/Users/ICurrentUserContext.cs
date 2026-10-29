using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Users;

public interface ICurrentUserContext
{
    User CurrentUser { get; }

    bool IsAdmin => CurrentUser.Role == UserRole.Admin;
}
