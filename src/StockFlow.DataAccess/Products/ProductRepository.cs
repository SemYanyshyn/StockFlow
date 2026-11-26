using Npgsql;
using StockFlow.Domain;

namespace StockFlow.DataAccess.Products;

public sealed class ProductRepository : IProductRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public ProductRepository(DbConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public IReadOnlyList<Product> GetAll()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand(
            "SELECT id, name, unit, category_id FROM products ORDER BY name", connection);
        using var reader = command.ExecuteReader();

        var products = new List<Product>();
        while (reader.Read())
        {
            products.Add(new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Unit = UnitOfMeasureColumn.Parse(reader.GetString(2)),
                CategoryId = reader.GetInt32(3)
            });
        }

        return products;
    }

    public int Add(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand(
            "INSERT INTO products (name, unit, category_id) VALUES (@name, @unit, @category_id) RETURNING id",
            connection);
        command.Parameters.AddWithValue("name", product.Name);
        command.Parameters.AddWithValue("unit", UnitOfMeasureColumn.Format(product.Unit));
        command.Parameters.AddWithValue("category_id", product.CategoryId);

        return (int)command.ExecuteScalar()!;
    }

    public Product? GetById(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand(
            "SELECT id, name, unit, category_id FROM products WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", productId);
        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return new Product
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Unit = UnitOfMeasureColumn.Parse(reader.GetString(2)),
            CategoryId = reader.GetInt32(3)
        };
    }

    public bool Update(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand(
            "UPDATE products SET name = @name, unit = @unit, category_id = @category_id WHERE id = @id", connection);
        command.Parameters.AddWithValue("name", product.Name);
        command.Parameters.AddWithValue("unit", UnitOfMeasureColumn.Format(product.Unit));
        command.Parameters.AddWithValue("category_id", product.CategoryId);
        command.Parameters.AddWithValue("id", product.Id);

        return command.ExecuteNonQuery() == 1;
    }

    public bool HasRelatedRecords(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM stock WHERE product_id = @id) " +
            "OR EXISTS (SELECT 1 FROM stock_movements WHERE product_id = @id)", connection);
        command.Parameters.AddWithValue("id", productId);
        return (bool)command.ExecuteScalar()!;
    }

    public bool Delete(int productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand("DELETE FROM products WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", productId);

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
