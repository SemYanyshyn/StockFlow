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

    public IReadOnlyList<User> GetAll()
    {
        using var connection = _factory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand(
            "SELECT id, username, full_name, role, is_active FROM users ORDER BY username", connection);
        using var reader = command.ExecuteReader();
        var users = new List<User>();
        while (reader.Read()) users.Add(new User
        {
            Id = reader.GetInt32(0), Username = reader.GetString(1), FullName = reader.GetString(2),
            Role = ParseRole(reader.GetString(3)), IsActive = reader.GetBoolean(4)
        });
        return users;
    }

    public IUserManagementTransaction BeginManagement() => new ManagementTransaction(_factory);

    private static UserRole ParseRole(string role) => role switch
    {
        "admin" => UserRole.Admin, "employee" => UserRole.Employee,
        _ => throw new InvalidOperationException("Невідома роль користувача в базі даних.")
    };

    private sealed class ManagementTransaction : IUserManagementTransaction
    {
        private readonly NpgsqlConnection _connection;
        private readonly NpgsqlTransaction _transaction;

        public ManagementTransaction(DbConnectionFactory factory)
        {
            _connection = factory.CreateConnection();
            try
            {
                _connection.Open();
                _transaction = _connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
                // Serializes all writers (including two app instances); ordinary SELECT/login remains available.
                // The lock is acquired in a separate statement, so subsequent reads see the preceding commit.
                using var command = Command("LOCK TABLE users IN SHARE ROW EXCLUSIVE MODE");
                command.ExecuteNonQuery();
            }
            catch { _connection.Dispose(); throw; }
        }

        private NpgsqlCommand Command(string sql) => new(sql, _connection, _transaction);
        public IReadOnlyList<User> GetAll()
        {
            using var command = Command("SELECT id, username, full_name, role, is_active FROM users ORDER BY username");
            using var reader = command.ExecuteReader();
            var users = new List<User>();
            while (reader.Read()) users.Add(new User
            {
                Id = reader.GetInt32(0), Username = reader.GetString(1), FullName = reader.GetString(2),
                Role = ParseRole(reader.GetString(3)), IsActive = reader.GetBoolean(4)
            });
            return users;
        }
        public User? GetById(int id)
        {
            using var command = Command("SELECT id, username, password_hash, full_name, role, is_active FROM users WHERE id=@id");
            command.Parameters.AddWithValue("id", id);
            using var reader = command.ExecuteReader();
            return reader.Read() ? Read(reader) : null;
        }
        public int CountActiveAdmins()
        {
            using var command = Command("SELECT COUNT(*) FROM users WHERE role='admin' AND is_active");
            return Convert.ToInt32(command.ExecuteScalar());
        }
        public bool HasMovements(int id)
        {
            using var command = Command("SELECT EXISTS(SELECT 1 FROM stock_movements WHERE user_id=@id)");
            command.Parameters.AddWithValue("id", id);
            return (bool)command.ExecuteScalar()!;
        }
        public int Add(User user)
        {
            using var command = Command("INSERT INTO users (username,password_hash,full_name,role,is_active) VALUES (@username,@hash,@name,@role,@active) RETURNING id");
            command.Parameters.AddWithValue("username", user.Username);
            command.Parameters.AddWithValue("hash", user.PasswordHash);
            command.Parameters.AddWithValue("name", user.FullName);
            command.Parameters.AddWithValue("role", user.Role == UserRole.Admin ? "admin" : "employee");
            command.Parameters.AddWithValue("active", user.IsActive);
            try { return (int)command.ExecuteScalar()!; }
            catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
            { throw new DuplicateUsernameException(); }
        }
        public bool Update(User user)
        {
            using var command = Command("UPDATE users SET full_name=@name, role=@role WHERE id=@id");
            command.Parameters.AddWithValue("id", user.Id);
            command.Parameters.AddWithValue("name", user.FullName);
            command.Parameters.AddWithValue("role", user.Role == UserRole.Admin ? "admin" : "employee");
            return command.ExecuteNonQuery() == 1;
        }
        public bool SetActive(int id, bool isActive)
        {
            using var command = Command("UPDATE users SET is_active=@active WHERE id=@id");
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("active", isActive);
            return command.ExecuteNonQuery() == 1;
        }
        public bool Delete(int id)
        {
            using var command = Command("DELETE FROM users WHERE id=@id");
            command.Parameters.AddWithValue("id", id);
            try { return command.ExecuteNonQuery() == 1; }
            catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            { throw new StockFlow.DataAccess.Products.RecordInUseException(); }
        }
        public void Commit() => _transaction.Commit();
        public void Dispose()
        {
            try { _transaction.Dispose(); }
            finally { _connection.Dispose(); }
        }
    }

    private static User Read(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0), Username = reader.GetString(1), PasswordHash = reader.GetString(2),
        FullName = reader.GetString(3),
        Role = ParseRole(reader.GetString(4)),
        IsActive = reader.GetBoolean(5)
    };
}
