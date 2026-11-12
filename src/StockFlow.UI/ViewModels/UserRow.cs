using StockFlow.Domain;

namespace StockFlow.UI.ViewModels;

public sealed record RoleOption(string Title, UserRole? Value);
public sealed record UserRow(User User)
{
    public int Id => User.Id;
    public string Username => User.Username;
    public string FullName => User.FullName;
    public string RoleText => User.Role == UserRole.Admin ? "Адміністратор" : "Працівник";
    public string StatusText => User.IsActive ? "Активний" : "Заблокований";
    public string ToggleText => User.IsActive ? "Заблокувати" : "Розблокувати";
}
