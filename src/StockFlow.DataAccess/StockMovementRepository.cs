using System.Data;
using Npgsql;
using StockFlow.Domain;

namespace StockFlow.DataAccess;

// Uses the merged team schema. It does not own the supplied
// transaction or connection; the service decides commit/rollback and disposes them.
public sealed class StockMovementRepository : IStockMovementRepository
{
    private const string NumberConstraint = "stock_movements_number_key";

    public int Add(StockMovement movement, IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(transaction);

        var databaseType = movement.Type switch
        {
            MovementType.Receipt => "receipt",
            MovementType.WriteOff => "write_off",
            _ => throw new ArgumentOutOfRangeException(nameof(movement), "Невідомий тип руху товару")
        };

        var connection = transaction.Connection
            ?? throw new InvalidOperationException("Транзакція не має активного підключення");
        if (connection.State != ConnectionState.Open)
            throw new InvalidOperationException("Підключення транзакції має бути відкритим");

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;
        command.CommandText = """
            INSERT INTO stock_movements
                (number, type, product_id, warehouse_id, supplier_id, user_id,
                 quantity, movement_date, note)
            VALUES
                (@number, @type, @product_id, @warehouse_id, @supplier_id, @user_id,
                 @quantity, @movement_date, @note)
            RETURNING id;
            """;

        AddParameter(command, "number", DbType.String, movement.Number);
        AddParameter(command, "type", DbType.String, databaseType);
        AddParameter(command, "product_id", DbType.Int32, movement.ProductId);
        AddParameter(command, "warehouse_id", DbType.Int32, movement.WarehouseId);
        AddParameter(command, "supplier_id", DbType.Int32, movement.SupplierId);
        AddParameter(command, "user_id", DbType.Int32, movement.UserId);
        AddParameter(command, "quantity", DbType.Int32, movement.Quantity);

        // The team's schema uses TIMESTAMP WITHOUT TIME ZONE. DateTime2 selects
        // that Npgsql type; preserve wall-clock fields, without timezone conversion.
        // https://www.npgsql.org/doc/types/basic.html
        AddParameter(command, "movement_date", DbType.DateTime2,
            DateTime.SpecifyKind(movement.MovementDate, DateTimeKind.Unspecified));
        AddParameter(command, "note", DbType.String, movement.Note);

        try
        {
            if (command.ExecuteScalar() is not int id || id <= 0)
                throw new InvalidOperationException("Не вдалося отримати ідентифікатор руху товару");
            return id;
        }
        catch (PostgresException error) when (
            error.SqlState == PostgresErrorCodes.UniqueViolation &&
            error.ConstraintName == NumberConstraint)
        {
            // Do not retain provider details, SQL or parameter values as an inner exception.
            throw new DuplicateNumberException();
        }
    }

    private static void AddParameter(IDbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
