using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Users;

public sealed class TemporaryAdminUserContext : ICurrentUserContext
{
    public User CurrentUser { get; } = new()
    {
        Id = 1,
        Username = "admin",
        FullName = "Адміністратор",
        Role = UserRole.Admin,
        IsActive = true
    };
}
