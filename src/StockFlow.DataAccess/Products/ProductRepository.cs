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
}
