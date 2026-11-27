using Npgsql;

namespace StockFlow.DataAccess;

public sealed class StockMovementNumberRepository(DbConnectionFactory connections)
{
    public string Reserve()
    {
        using var connection = connections.CreateConnection();
        connection.Open();
        // nextval is atomic between sessions. The ID sequence can have gaps when forms are cancelled.
        // Existing manually named invoices may use the same format, so skip names already in the table.
        using var command = new NpgsqlCommand("""
            WITH candidate AS (
                SELECT nextval(pg_get_serial_sequence('stock_movements', 'id')::regclass)::text AS value
            ), formatted AS (
                SELECT 'НК-' || lpad(value, GREATEST(3, length(value)), '0') AS number FROM candidate
            )
            SELECT f.number FROM formatted f
            WHERE NOT EXISTS (SELECT 1 FROM stock_movements m WHERE m.number = f.number)
            """, connection);
        for (var attempt = 0; attempt < 100; attempt++)
            if (command.ExecuteScalar() is string number) return number;
        throw new InvalidOperationException("Не вдалося підібрати вільний номер накладної.");
    }
}
