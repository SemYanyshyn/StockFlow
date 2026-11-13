using System.Collections;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace StockFlow.Tests.Repository;

// Test-only recording ADO.NET objects. They never connect to a database and
// establish command/ownership behavior, not PostgreSQL SQL correctness or atomicity.
internal sealed class RecordingConnection : IDbConnection
{
    [AllowNull]
    public string ConnectionString { get; set; } = string.Empty;
    public int ConnectionTimeout => 0;
    public string Database => "isolated-test-double";
    public ConnectionState State { get; set; } = ConnectionState.Open;
    public int OpenCalls { get; private set; }
    public int CloseCalls { get; private set; }
    public int DisposeCalls { get; private set; }
    public object? ScalarResult { get; set; } = 42;
    public Exception? ExecuteError { get; set; }
    public List<RecordingCommand> Commands { get; } = [];

    public IDbCommand CreateCommand()
    {
        var command = new RecordingCommand(this);
        Commands.Add(command);
        return command;
    }

    public void Open() { OpenCalls++; State = ConnectionState.Open; }
    public void Close() { CloseCalls++; State = ConnectionState.Closed; }
    public void Dispose() => DisposeCalls++;
    public IDbTransaction BeginTransaction() => throw new NotSupportedException();
    public IDbTransaction BeginTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
    public void ChangeDatabase(string databaseName) => throw new NotSupportedException();
}

internal sealed class RecordingTransaction(IDbConnection? connection) : IDbTransaction
{
    public IDbConnection? Connection { get; } = connection;
    public IsolationLevel IsolationLevel => IsolationLevel.Serializable;
    public int CommitCalls { get; private set; }
    public int RollbackCalls { get; private set; }
    public int DisposeCalls { get; private set; }
    public void Commit() => CommitCalls++;
    public void Rollback() => RollbackCalls++;
    public void Dispose() => DisposeCalls++;
}

internal sealed class RecordingCommand(RecordingConnection owner) : IDbCommand
{
    [AllowNull]
    public string CommandText { get; set; } = string.Empty;
    public int CommandTimeout { get; set; }
    public CommandType CommandType { get; set; }
    public IDbConnection? Connection { get; set; } = owner;
    public IDataParameterCollection Parameters { get; } = new RecordingParameters();
    public IDbTransaction? Transaction { get; set; }
    public UpdateRowSource UpdatedRowSource { get; set; }
    public bool Disposed { get; private set; }
    public int ExecuteCalls { get; private set; }

    public object? ExecuteScalar()
    {
        ExecuteCalls++;
        if (owner.ExecuteError is not null) throw owner.ExecuteError;
        return owner.ScalarResult;
    }

    public IDbDataParameter CreateParameter() => new RecordingParameter();
    public void Dispose() => Disposed = true;
    public void Cancel() => throw new NotSupportedException();
    public void Prepare() => throw new NotSupportedException();
    public int ExecuteNonQuery() => throw new NotSupportedException();
    public IDataReader ExecuteReader() => throw new NotSupportedException();
    public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
}

internal sealed class RecordingParameter : IDbDataParameter
{
    public DbType DbType { get; set; }
    public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public bool IsNullable => true;
    [AllowNull]
    public string ParameterName { get; set; } = string.Empty;
    [AllowNull]
    public string SourceColumn { get; set; } = string.Empty;
    public DataRowVersion SourceVersion { get; set; }
    public object? Value { get; set; }
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public int Size { get; set; }
}

internal sealed class RecordingParameters : ArrayList, IDataParameterCollection
{
    public object this[string parameterName]
    {
        get { var index = IndexOf(parameterName); return index < 0 ? throw new IndexOutOfRangeException(parameterName) : this[index]!; }
        set { var index = IndexOf(parameterName); if (index < 0) Add(value); else this[index] = value; }
    }

    public bool Contains(string parameterName) => IndexOf(parameterName) >= 0;

    public int IndexOf(string parameterName)
    {
        for (var index = 0; index < Count; index++)
            if (this[index] is IDataParameter parameter && parameter.ParameterName == parameterName)
                return index;
        return -1;
    }

    public void RemoveAt(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index >= 0) RemoveAt(index);
    }
}
