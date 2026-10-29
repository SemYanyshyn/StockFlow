using Microsoft.Extensions.Configuration;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Users;

public sealed class TemporaryUserContext : ICurrentUserContext
{
    public const string RoleKey = "StockFlow:CurrentRole";

    public TemporaryUserContext(UserRole role)
    {
        CurrentUser = role switch
        {
            UserRole.Admin => new User { Id = 1, Username = "admin", FullName = "Адміністратор", Role = UserRole.Admin },
            UserRole.Employee => new User { Id = 2, Username = "employee", FullName = "Працівник складу", Role = UserRole.Employee },
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
    }

    public User CurrentUser { get; }

    public static TemporaryUserContext FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var value = configuration[RoleKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return new TemporaryUserContext(UserRole.Admin);
        }

        var name = value.Trim();
        if (name.Equals(nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase))
        {
            return new TemporaryUserContext(UserRole.Admin);
        }

        if (name.Equals(nameof(UserRole.Employee), StringComparison.OrdinalIgnoreCase))
        {
            return new TemporaryUserContext(UserRole.Employee);
        }

        throw new InvalidOperationException(
            $"Некоректне значення {RoleKey}. Допустимі значення: Admin або Employee.");
    }
}
