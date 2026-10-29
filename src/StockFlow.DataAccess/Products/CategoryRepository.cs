using Npgsql;
using StockFlow.Domain;

namespace StockFlow.DataAccess.Products;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public CategoryRepository(DbConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public IReadOnlyList<Category> GetAll()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand("SELECT id, name FROM categories ORDER BY name", connection);
        using var reader = command.ExecuteReader();

        var categories = new List<Category>();
        while (reader.Read())
        {
            categories.Add(new Category { Id = reader.GetInt32(0), Name = reader.GetString(1) });
        }

        return categories;
    }

    public int Add(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand("INSERT INTO categories (name) VALUES (@name) RETURNING id", connection);
        command.Parameters.AddWithValue("name", category.Name);

        try
        {
            return (int)command.ExecuteScalar()!;
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateNameException();
        }
    }
}
