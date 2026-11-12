using Npgsql;
using StockFlow.Domain;

namespace StockFlow.DataAccess.Users;

public sealed class UserRepository : IUserRepository
{
    private readonly DbConnectionFactory _factory;
    public UserRepository(DbConnectionFactory factory) => _factory = factory;

    public User? GetByUsername(string username)
    {
        using var connection = _factory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand(
            "SELECT id, username, password_hash, full_name, role, is_active FROM users WHERE username=@username", connection);
        command.Parameters.AddWithValue("username", username);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    private static User Read(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0), Username = reader.GetString(1), PasswordHash = reader.GetString(2),
        FullName = reader.GetString(3),
        Role = reader.GetString(4) switch
        {
            "admin" => UserRole.Admin, "employee" => UserRole.Employee,
            _ => throw new InvalidOperationException("Невідома роль користувача в базі даних.")
        },
        IsActive = reader.GetBoolean(5)
    };
}
