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

    public bool Update(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand("UPDATE categories SET name = @name WHERE id = @id", connection);
        command.Parameters.AddWithValue("name", category.Name);
        command.Parameters.AddWithValue("id", category.Id);

        try
        {
            return command.ExecuteNonQuery() == 1;
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateNameException();
        }
    }

    public int CountProducts(int categoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand("SELECT COUNT(*) FROM products WHERE category_id = @id", connection);
        command.Parameters.AddWithValue("id", categoryId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public bool Delete(int categoryId)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand("DELETE FROM categories WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", categoryId);

        try
        {
            return command.ExecuteNonQuery() == 1;
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new RecordInUseException();
        }
    }
}
