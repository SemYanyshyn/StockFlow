using System;
using System.Collections.Generic;
using System.Data;
using StockFlow.DataAccess;
using StockFlow.Domain;

namespace StockFlow.DataAccess.Warehouses;

public class WarehouseRepository : IWarehouseRepository
{
    private readonly DbConnectionFactory _connectionFactory;

    public WarehouseRepository(DbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public IReadOnlyList<Warehouse> GetAll()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, address FROM warehouses ORDER BY name;";

        using var reader = command.ExecuteReader();
        var list = new List<Warehouse>();
        while (reader.Read())
        {
            list.Add(new Warehouse
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Address = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }
        return list;
    }

    public int Add(Warehouse warehouse)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO warehouses (name, address) 
            VALUES (@name, @address) 
            RETURNING id;";

        var pName = command.CreateParameter();
        pName.ParameterName = "@name";
        pName.Value = warehouse.Name;
        command.Parameters.Add(pName);

        var pAddress = command.CreateParameter();
        pAddress.ParameterName = "@address";
        pAddress.Value = (object?)warehouse.Address ?? DBNull.Value;
        command.Parameters.Add(pAddress);

        return Convert.ToInt32(command.ExecuteScalar());
    }
}