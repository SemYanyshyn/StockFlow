using Npgsql;
using NpgsqlTypes;
using StockFlow.Domain;

namespace StockFlow.DataAccess.Suppliers;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public SupplierRepository(DbConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public IReadOnlyList<Supplier> GetAll()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand(
            "SELECT id, name, phone, email FROM suppliers ORDER BY name", connection);
        using var reader = command.ExecuteReader();

        var suppliers = new List<Supplier>();
        while (reader.Read())
        {
            suppliers.Add(new Supplier
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return suppliers;
    }

    public int Add(Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(supplier);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = new NpgsqlCommand(
            "INSERT INTO suppliers (name, phone, email) " +
            "VALUES (@name, @phone, @email) RETURNING id", connection);

        command.Parameters.AddWithValue("name", supplier.Name);
        command.Parameters.Add("phone", NpgsqlDbType.Varchar).Value =
            (object?)supplier.Phone ?? DBNull.Value;
        command.Parameters.Add("email", NpgsqlDbType.Varchar).Value =
            (object?)supplier.Email ?? DBNull.Value;

        return (int)command.ExecuteScalar()!;
    }
}
