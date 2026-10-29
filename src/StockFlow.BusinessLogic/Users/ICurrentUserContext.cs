using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Users;

public interface ICurrentUserContext
{
    User CurrentUser { get; }

    int UserId => CurrentUser.Id;

    UserRole Role => CurrentUser.Role;

    bool IsAdmin => Role == UserRole.Admin;
}
