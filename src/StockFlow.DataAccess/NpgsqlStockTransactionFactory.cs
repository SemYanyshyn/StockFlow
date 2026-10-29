using System.Data;
using Npgsql;

namespace StockFlow.DataAccess;

/// <summary>Candidate Serializable boundary; requires team approval before production integration.</summary>
public sealed class NpgsqlStockTransactionFactory(DbConnectionFactory connections) : IStockTransactionFactory
{
    public IStockTransaction Begin()
    {
        var connection = connections.CreateConnection();
        try
        {
            connection.Open();
            var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
            return new Scope(connection, transaction);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private sealed class Scope(NpgsqlConnection connection, NpgsqlTransaction transaction) : IStockTransaction
    {
        private bool _completed;
        private bool _disposed;
        public IDbTransaction Transaction => transaction;
        public bool IsActive => !_completed && !_disposed;

        public void Commit()
        {
            transaction.Commit();
            _completed = true;
        }

        public void Rollback()
        {
            if (!IsActive) return;
            transaction.Rollback();
            _completed = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                if (IsActive) Rollback();
            }
            finally
            {
                _disposed = true;
                try { transaction.Dispose(); }
                finally { connection.Dispose(); }
            }
        }
    }
}
