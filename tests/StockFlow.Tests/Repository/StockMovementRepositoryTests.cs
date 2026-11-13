using System.Data;
using Npgsql;
using StockFlow.DataAccess;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests.Repository;

public sealed class StockMovementRepositoryTests
{
    private static StockMovement Receipt() => new()
    {
        Number = "НК-017",
        Type = MovementType.Receipt,
        ProductId = 11,
        WarehouseId = 12,
        SupplierId = 13,
        UserId = 14,
        Quantity = 8,
        MovementDate = new DateTime(2026, 10, 30, 12, 34, 56),
        Note = "Нове надходження"
    };

    private static IDbDataParameter Parameter(RecordingCommand command, string name) =>
        Assert.IsAssignableFrom<IDbDataParameter>(command.Parameters[name]);

    [Fact]
    public void ReceiptUsesSuppliedTransactionAndAllFieldValuesAndReturnsId()
    {
        var movement = Receipt();
        var connection = new RecordingConnection { ScalarResult = 73 };
        var transaction = new RecordingTransaction(connection);

        var id = new StockMovementRepository().Add(movement, transaction);

        Assert.Equal(73, id);
        Assert.Equal(0, movement.Id);
        var command = Assert.Single(connection.Commands);
        Assert.Same(connection, command.Connection);
        Assert.Same(transaction, command.Transaction);
        Assert.Equal(CommandType.Text, command.CommandType);
        Assert.Equal(9, command.Parameters.Count);
        Assert.Equal(movement.Number, Parameter(command, "number").Value);
        Assert.Equal("receipt", Parameter(command, "type").Value);
        Assert.Equal(movement.ProductId, Parameter(command, "product_id").Value);
        Assert.Equal(movement.WarehouseId, Parameter(command, "warehouse_id").Value);
        Assert.Equal(movement.SupplierId, Parameter(command, "supplier_id").Value);
        Assert.Equal(movement.UserId, Parameter(command, "user_id").Value);
        Assert.Equal(movement.Quantity, Parameter(command, "quantity").Value);
        Assert.Equal(movement.MovementDate, Parameter(command, "movement_date").Value);
        Assert.Equal(movement.Note, Parameter(command, "note").Value);
        Assert.Equal(1, command.ExecuteCalls);
        Assert.True(command.Disposed);
        AssertOwnershipPreserved(connection, transaction);
    }

    [Fact]
    public void WriteOffMapsExplicitlyAndNullsKeepTheirDatabaseTypes()
    {
        var movement = Receipt();
        movement.Type = MovementType.WriteOff;
        movement.SupplierId = null;
        movement.Note = null;
        var connection = new RecordingConnection();

        new StockMovementRepository().Add(movement, new RecordingTransaction(connection));

        var command = Assert.Single(connection.Commands);
        Assert.Equal("write_off", Parameter(command, "type").Value);
        Assert.Same(DBNull.Value, Parameter(command, "supplier_id").Value);
        Assert.Equal(DbType.Int32, Parameter(command, "supplier_id").DbType);
        Assert.Same(DBNull.Value, Parameter(command, "note").Value);
        Assert.Equal(DbType.String, Parameter(command, "note").DbType);
    }

    [Fact]
    public void UserValuesAreParametersAndNeverInterpolatedIntoCommandText()
    {
        var movement = Receipt();
        movement.Number = "НК-017'); --";
        movement.Note = "'; DROP TABLE stock_movements; --";
        var connection = new RecordingConnection();

        new StockMovementRepository().Add(movement, new RecordingTransaction(connection));

        var command = Assert.Single(connection.Commands);
        Assert.DoesNotContain(movement.Number, command.CommandText);
        Assert.DoesNotContain(movement.Note, command.CommandText);
        Assert.Equal(movement.Number, Parameter(command, "number").Value);
        Assert.Equal(movement.Note, Parameter(command, "note").Value);
        Assert.Contains("RETURNING id", command.CommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void TimestampWithoutTimezonePreservesClockFieldsAndSelectsDateTime2(DateTimeKind kind)
    {
        var movement = Receipt();
        movement.MovementDate = DateTime.SpecifyKind(movement.MovementDate, kind);
        var connection = new RecordingConnection();

        new StockMovementRepository().Add(movement, new RecordingTransaction(connection));

        var parameter = Parameter(Assert.Single(connection.Commands), "movement_date");
        var sent = Assert.IsType<DateTime>(parameter.Value);
        Assert.Equal(DbType.DateTime2, parameter.DbType);
        Assert.Equal(movement.MovementDate.Ticks, sent.Ticks);
        Assert.Equal(DateTimeKind.Unspecified, sent.Kind);
        Assert.Equal(kind, movement.MovementDate.Kind);
    }

    [Fact]
    public void UnknownMovementTypeIsRejectedBeforeCreatingACommand()
    {
        var movement = Receipt();
        movement.Type = (MovementType)999;
        var connection = new RecordingConnection();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StockMovementRepository().Add(movement, new RecordingTransaction(connection)));
        Assert.Empty(connection.Commands);
    }

    [Fact]
    public void NullMovementIsRejectedBeforeCreatingACommand()
    {
        var connection = new RecordingConnection();
        Assert.Throws<ArgumentNullException>(() =>
            new StockMovementRepository().Add(null!, new RecordingTransaction(connection)));
        Assert.Empty(connection.Commands);
    }

    [Fact]
    public void NullTransactionIsRejected() => Assert.Throws<ArgumentNullException>(() =>
        new StockMovementRepository().Add(Receipt(), null!));

    [Fact]
    public void CompletedTransactionWithoutConnectionIsRejected() => Assert.Throws<InvalidOperationException>(() =>
        new StockMovementRepository().Add(Receipt(), new RecordingTransaction(null)));

    [Fact]
    public void RepositoryDoesNotOpenAClosedTransactionConnection()
    {
        var connection = new RecordingConnection { State = ConnectionState.Closed };
        var transaction = new RecordingTransaction(connection);
        Assert.Throws<InvalidOperationException>(() => new StockMovementRepository().Add(Receipt(), transaction));
        Assert.Empty(connection.Commands);
        AssertOwnershipPreserved(connection, transaction);
    }

    [Fact]
    public void NumberConstraintViolationBecomesSafeDuplicateNumberException()
    {
        const string sensitive = "test-only-provider-sensitive-details";
        var connection = new RecordingConnection
        {
            ExecuteError = new PostgresException(sensitive, "ERROR", "ERROR", "23505",
                constraintName: "stock_movements_number_key")
        };
        var transaction = new RecordingTransaction(connection);

        var error = Assert.Throws<DuplicateNumberException>(() =>
            new StockMovementRepository().Add(Receipt(), transaction));

        Assert.Equal("Номер накладної вже існує", error.Message);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain(sensitive, error.ToString());
        Assert.True(Assert.Single(connection.Commands).Disposed);
        AssertOwnershipPreserved(connection, transaction);
    }

    [Theory]
    [InlineData("stock_pkey")]
    [InlineData("users_username_key")]
    [InlineData(null)]
    public void OtherUniqueViolationsAreNotMisreportedAsDuplicateNumber(string? constraint)
    {
        var providerError = new PostgresException("synthetic unique violation", "ERROR", "ERROR", "23505",
            constraintName: constraint);
        var connection = new RecordingConnection { ExecuteError = providerError };
        var transaction = new RecordingTransaction(connection);

        var error = Assert.Throws<PostgresException>(() =>
            new StockMovementRepository().Add(Receipt(), transaction));

        Assert.Same(providerError, error);
        Assert.True(Assert.Single(connection.Commands).Disposed);
        AssertOwnershipPreserved(connection, transaction);
    }

    [Theory]
    [InlineData("40001")]
    [InlineData("23503")]
    [InlineData("23514")]
    public void NonUniqueProviderErrorsReachTheServiceUnchanged(string state)
    {
        var providerError = new PostgresException("synthetic provider failure", "ERROR", "ERROR", state,
            constraintName: "stock_movements_number_key");
        var connection = new RecordingConnection { ExecuteError = providerError };
        var transaction = new RecordingTransaction(connection);

        var error = Assert.Throws<PostgresException>(() =>
            new StockMovementRepository().Add(Receipt(), transaction));

        Assert.Same(providerError, error);
        Assert.True(Assert.Single(connection.Commands).Disposed);
        AssertOwnershipPreserved(connection, transaction);
    }

    [Fact]
    public void UnexpectedExecutionFailureStillDisposesOnlyTheCommand()
    {
        var failure = new InvalidOperationException("synthetic execution failure");
        var connection = new RecordingConnection { ExecuteError = failure };
        var transaction = new RecordingTransaction(connection);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
            new StockMovementRepository().Add(Receipt(), transaction)));
        Assert.True(Assert.Single(connection.Commands).Disposed);
        AssertOwnershipPreserved(connection, transaction);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData("not-an-id")]
    public void MissingOrInvalidReturnedIdIsRejected(object? scalar)
    {
        var connection = new RecordingConnection { ScalarResult = scalar };
        var transaction = new RecordingTransaction(connection);
        var error = Assert.Throws<InvalidOperationException>(() =>
            new StockMovementRepository().Add(Receipt(), transaction));
        Assert.Contains("ідентифікатор", error.Message);
        Assert.True(Assert.Single(connection.Commands).Disposed);
        AssertOwnershipPreserved(connection, transaction);
    }

    private static void AssertOwnershipPreserved(RecordingConnection connection, RecordingTransaction transaction)
    {
        Assert.Equal(0, connection.OpenCalls);
        Assert.Equal(0, connection.CloseCalls);
        Assert.Equal(0, connection.DisposeCalls);
        Assert.Equal(0, transaction.CommitCalls);
        Assert.Equal(0, transaction.RollbackCalls);
        Assert.Equal(0, transaction.DisposeCalls);
    }
}
