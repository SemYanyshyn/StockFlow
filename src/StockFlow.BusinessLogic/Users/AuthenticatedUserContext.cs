using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Users;

public sealed class AuthenticatedUserContext : ICurrentUserContext
{
    private User? _user;
    public event Action? UserChanged;
    public bool IsAuthenticated => _user is not null;
    // Return a snapshot without credentials: callers cannot mutate session permissions.
    public User CurrentUser => _user is { } user ? Snapshot(user) :
        throw new InvalidOperationException("Спочатку увійдіть у систему.");

    internal void SignIn(User user)
    {
        _user = Snapshot(user);
        UserChanged?.Invoke();
    }
    internal void Clear() => _user = null;
    private static User Snapshot(User user) => new()
    {
        Id = user.Id, Username = user.Username, FullName = user.FullName,
        Role = user.Role, IsActive = user.IsActive
    };
}
