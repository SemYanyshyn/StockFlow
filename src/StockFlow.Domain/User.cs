namespace StockFlow.Domain;

public class User
{
    public const int MaxUsernameLength = 50;
    public const int MaxFullNameLength = 100;

    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    public bool Validate() =>
        !string.IsNullOrWhiteSpace(Username) &&
        Username.Trim().Length <= MaxUsernameLength &&
        !string.IsNullOrWhiteSpace(PasswordHash) &&
        !string.IsNullOrWhiteSpace(FullName) &&
        FullName.Trim().Length <= MaxFullNameLength &&
        Enum.IsDefined(Role);
}
