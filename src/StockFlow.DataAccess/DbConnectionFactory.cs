using Microsoft.Extensions.Configuration;
using Npgsql;

namespace StockFlow.DataAccess;

/// <summary>Creates independent, closed connections. The caller opens and disposes each one.</summary>
public sealed class DbConnectionFactory
{
    private const string InvalidConfiguration =
        "Некоректне ConnectionStrings:StockFlow. Перевірте Host, Port (1-65535), " +
        "Database та Username і замініть демонстраційні значення у локальній конфігурації.";

    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var value = configuration.GetConnectionString("StockFlow");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "Не задано ConnectionStrings:StockFlow. Налаштуйте локальний appsettings.json " +
                "або змінну STOCKFLOW_ConnectionStrings__StockFlow.");
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(value);
            if (string.IsNullOrWhiteSpace(builder.Host) ||
                string.IsNullOrWhiteSpace(builder.Database) ||
                string.IsNullOrWhiteSpace(builder.Username) ||
                builder.Port is < 1 or > 65535 ||
                builder.Password == "<set-locally>")
            {
                throw new InvalidOperationException(InvalidConfiguration);
            }

            _connectionString = builder.ConnectionString;
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
        {
            // Provider exceptions can include configuration values. Do not retain them as InnerException.
            throw new InvalidOperationException(InvalidConfiguration);
        }
    }

    /// <summary>
    /// No network I/O occurs here. Use using/await using, then Open/OpenAsync for a data operation.
    /// A service owns any transaction shared by repositories; this factory never starts one.
    /// </summary>
    public NpgsqlConnection CreateConnection() => new(_connectionString);
}
