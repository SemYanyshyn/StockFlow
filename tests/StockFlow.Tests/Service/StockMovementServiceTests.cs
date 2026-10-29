using System.Data;
using Npgsql;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests.Service;

/// <summary>
/// Exercises orchestration against stateful transactional fakes only.
/// Passing these tests does not prove PostgreSQL isolation, rollback, or concurrency.
/// </summary>
public sealed class StockMovementServiceTests
{
    private const string SensitiveMarker = "test-only-provider-sensitive-detail";

    [Theory]
    [InlineData(MovementType.Receipt, 5, 15)]
    [InlineData(MovementType.WriteOff, 4, 6)]
    public void SuccessfulMovementAddsAndSavesInOneTransactionThenCommits(
        MovementType type, int quantity, int expectedQuantity)
    {
        var fixture = new Fixture();
        var result = fixture.Service.Register(Request(type, quantity));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.MovementId);
        Assert.Null(result.Error);
        Assert.Equal(1, fixture.Transactions.BeginCount);
        Assert.Equal(1, fixture.Stocks.ReadCount);
        Assert.Equal((11, 22), fixture.Stocks.LastRequestedPair);
        Assert.Equal(1, fixture.Movements.AddCount);
        Assert.Equal(1, fixture.Stocks.SaveCount);
        Assert.Same(fixture.Transaction.Transaction, fixture.Stocks.ReadTransaction);
        Assert.Same(fixture.Transaction.Transaction, fixture.Stocks.SaveTransaction);
        Assert.Same(fixture.Transaction.Transaction, fixture.Movements.AddTransaction);
        Assert.Equal(1, fixture.Transaction.CommitCount);
        Assert.Equal(0, fixture.Transaction.RollbackCount);
        Assert.Equal(expectedQuantity, fixture.State.PersistedStock.Quantity);
        Assert.Equal(3, fixture.State.PersistedStock.MinThreshold);
        Assert.Single(fixture.State.PersistedMovements);
        AssertReleased(fixture);
    }

    [Fact]
    public void RegisterUsesCurrentUserInsteadOfEditableRequestUserId()
    {
        var fixture = new Fixture();
        var request = Request();
        request.UserId = 999;

        var result = fixture.Service.Register(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, Assert.Single(fixture.State.PersistedMovements).UserId);
        AssertReleased(fixture);
    }

    [Fact]
    public void AdminIsRejectedBeforeOpeningTransactionOrReadingData()
    {
        var fixture = new Fixture(UserRole.Admin);

        var result = fixture.Service.Register(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal("Створювати рух товару може лише Працівник", result.Error);
        Assert.Null(result.MovementId);
        Assert.Equal(0, fixture.Transactions.BeginCount);
        Assert.Equal(0, fixture.Stocks.ReadCount);
        Assert.Equal(0, fixture.Movements.AddCount);
        Assert.Equal(0, fixture.Stocks.SaveCount);
        Assert.Empty(fixture.State.PersistedMovements);
    }

    [Fact]
    public void InsufficientStockDoesNotAddOrSaveAndReleasesTransaction()
    {
        var fixture = new Fixture();

        var result = fixture.Service.Register(Request(MovementType.WriteOff, 11));

        Assert.False(result.IsSuccess);
        Assert.Equal("Недостатньо товару на складі", result.Error);
        Assert.Equal(0, fixture.Movements.AddCount);
        Assert.Equal(0, fixture.Stocks.SaveCount);
        Assert.Equal(0, fixture.Transaction.CommitCount);
        Assert.Equal(1, fixture.Transaction.RollbackCount);
        Assert.Equal(10, fixture.State.PersistedStock.Quantity);
        Assert.Empty(fixture.State.PersistedMovements);
        AssertReleased(fixture);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositiveQuantityReturnsDomainMessageWithoutPersisting(int quantity)
    {
        var fixture = new Fixture();

        var result = fixture.Service.Register(Request(MovementType.Receipt, quantity));

        Assert.False(result.IsSuccess);
        Assert.Equal("Кількість має бути більшою за 0", result.Error);
        Assert.Equal(0, fixture.Movements.AddCount);
        Assert.Equal(0, fixture.Stocks.SaveCount);
        Assert.Equal(0, fixture.Transaction.CommitCount);
        Assert.Equal(10, fixture.State.PersistedStock.Quantity);
        Assert.Empty(fixture.State.PersistedMovements);
        AssertEveryOpenedResourceReleased(fixture);
    }

    [Fact]
    public void ReceiptWithoutSupplierReturnsDomainMessageWithoutPersisting()
    {
        var fixture = new Fixture();
        var request = Request();
        request.SupplierId = null;

        var result = fixture.Service.Register(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("Для надходження потрібно обрати постачальника", result.Error);
        Assert.Equal(0, fixture.Movements.AddCount);
        Assert.Equal(0, fixture.Stocks.SaveCount);
        Assert.Equal(0, fixture.Transaction.CommitCount);
        Assert.Empty(fixture.State.PersistedMovements);
        AssertEveryOpenedResourceReleased(fixture);
    }

    [Fact]
    public void SaveFailureRollsBackStagedFakeMovementAndStock()
    {
        var fixture = new Fixture();
        fixture.Stocks.SaveError = new InvalidOperationException(SensitiveMarker);

        var result = fixture.Service.Register(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal(1, fixture.Movements.AddCount);
        Assert.Equal(1, fixture.Stocks.SaveCount);
        Assert.Equal(1, fixture.Transaction.RollbackCount);
        Assert.Equal(0, fixture.Transaction.CommitCount);
        Assert.Equal(10, fixture.State.PersistedStock.Quantity);
        Assert.Empty(fixture.State.PersistedMovements);
        AssertSanitizedError(result.Error);
        AssertReleased(fixture);
    }

    [Fact]
    public void DuplicateNumberReturnsNumberSpecificMessageAndRollsBack()
    {
        var fixture = new Fixture();
        fixture.Movements.AddError = new DuplicateNumberException();

        var result = fixture.Service.Register(Request());

        Assert.False(result.IsSuccess);
        Assert.Contains("номер", Assert.IsType<string>(result.Error), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Stocks.SaveCount);
        Assert.Equal(0, fixture.Transaction.CommitCount);
        Assert.Equal(1, fixture.Transaction.RollbackCount);
        Assert.Empty(fixture.State.PersistedMovements);
        AssertReleased(fixture);
    }

    [Theory]
    [InlineData("40001")]
    [InlineData("40P01")]
    public void SerializationAndDeadlockConflictsRequestRetry(string sqlState)
    {
        var fixture = new Fixture();
        fixture.Stocks.ReadError = PostgreSqlError(sqlState);

        var result = fixture.Service.Register(Request());

        Assert.False(result.IsSuccess);
        Assert.Contains("Спробуйте ще раз", Assert.IsType<string>(result.Error));
        AssertSanitizedError(result.Error);
        Assert.Equal(0, fixture.Movements.AddCount);
        Assert.Equal(0, fixture.Stocks.SaveCount);
        Assert.Equal(0, fixture.Transaction.CommitCount);
        Assert.Equal(1, fixture.Transaction.RollbackCount);
        AssertReleased(fixture);
    }

    [Theory]
    [InlineData("08006")]
    [InlineData("08003")]
    [InlineData("57P01")]
    [InlineData("57P03")]
    public void ServerReportedConnectionFailureGivesConnectionGuidance(string sqlState)
    {
        var fixture = new Fixture();
        fixture.Stocks.ReadError = PostgreSqlError(sqlState);
        var result = fixture.Service.Register(Request());
        Assert.False(result.IsSuccess);
        Assert.Contains("Перевірте підключення", result.Error);
        AssertSanitizedError(result.Error);
        AssertReleased(fixture);
    }

    [Fact]
    public void UnrelatedUniqueViolationIsNotMisreportedAsDuplicateNumber()
    {
        var fixture = new Fixture();
        fixture.Stocks.SaveError = PostgreSqlError("23505", "stock_pkey");

        var result = fixture.Service.Register(Request());

        Assert.False(result.IsSuccess);
        AssertSanitizedError(result.Error);
        Assert.DoesNotContain("номер", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, fixture.Transaction.RollbackCount);
        Assert.Equal(0, fixture.Transaction.CommitCount);
        Assert.Empty(fixture.State.PersistedMovements);
        Assert.Equal(10, fixture.State.PersistedStock.Quantity);
        AssertReleased(fixture);
    }

    [Fact]
    public void ConnectionOpeningFailureIsSanitizedAndDoesNotAccessRepositories()
    {
        var fixture = new Fixture();
        fixture.Transactions.BeginError = new NpgsqlException(SensitiveMarker);

        var result = fixture.Service.Register(Request());

        Assert.False(result.IsSuccess);
        AssertSanitizedError(result.Error);
        AssertDatabaseErrorText(result.Error!);
        Assert.Equal(0, fixture.Stocks.ReadCount);
        Assert.Equal(0, fixture.Stocks.SaveCount);
        Assert.Equal(0, fixture.Movements.AddCount);
        Assert.Empty(fixture.State.PersistedMovements);
    }

    [Fact]
    public void ConnectionLossDuringSaveRollsBackAndReleasesResources()
    {
        var fixture = new Fixture();
        fixture.Stocks.SaveError = new NpgsqlException(SensitiveMarker);

        var result = fixture.Service.Register(Request());

        Assert.False(result.IsSuccess);
        AssertSanitizedError(result.Error);
        AssertDatabaseErrorText(result.Error!);
        Assert.Equal(1, fixture.Transaction.RollbackCount);
        Assert.Equal(0, fixture.Transaction.CommitCount);
        Assert.Empty(fixture.State.PersistedMovements);
        AssertReleased(fixture);
    }

    [Fact]
    public void CommitFailureRollsBackWithoutPublishingStagedFakeWrites()
    {
        var fixture = new Fixture();
        fixture.Transaction.CommitError = new NpgsqlException(SensitiveMarker);

        var result = fixture.Service.Register(Request());

        Assert.False(result.IsSuccess);
        AssertSanitizedError(result.Error);
        Assert.Equal(1, fixture.Transaction.CommitCount);
        Assert.Equal(1, fixture.Transaction.RollbackCount);
        Assert.Empty(fixture.State.PersistedMovements);
        Assert.Equal(10, fixture.State.PersistedStock.Quantity);
        AssertReleased(fixture);
    }

    [Fact]
    public void RollbackFailureDoesNotReplaceOriginalDuplicateNumberError()
    {
        var fixture = new Fixture();
        fixture.Movements.AddError = new DuplicateNumberException();
        fixture.Transaction.RollbackError = new InvalidOperationException(SensitiveMarker);

        var result = fixture.Service.Register(Request());

        Assert.False(result.IsSuccess);
        Assert.Contains("номер", Assert.IsType<string>(result.Error), StringComparison.OrdinalIgnoreCase);
        AssertSanitizedError(result.Error);
        Assert.Equal(1, fixture.Transaction.RollbackCount);
        Assert.Equal(0, fixture.Transaction.CommitCount);
        Assert.Empty(fixture.State.PersistedMovements);
        AssertReleased(fixture);
    }

    private static StockMovement Request(MovementType type = MovementType.Receipt, int quantity = 5) => new()
    {
        Number = "НК-017",
        Type = type,
        ProductId = 11,
        WarehouseId = 22,
        SupplierId = type == MovementType.Receipt ? 33 : null,
        UserId = 999,
        Quantity = quantity,
        MovementDate = new DateTime(2026, 10, 30),
        Note = "Тестова операція"
    };

    private static PostgresException PostgreSqlError(string code, string? constraint = null) =>
        new(SensitiveMarker, "ERROR", "ERROR", code, constraintName: constraint);

    private static void AssertSanitizedError(string? error)
    {
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.DoesNotContain(SensitiveMarker, error!);
        Assert.DoesNotContain("23505", error!);
        Assert.DoesNotContain("40001", error!);
        Assert.DoesNotContain("40P01", error!);
        Assert.DoesNotContain("stock_pkey", error!);
    }

    private static void AssertDatabaseErrorText(string error) => Assert.True(
        error.Contains("баз", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("підключ", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("з’єднан", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("з'єднан", StringComparison.OrdinalIgnoreCase),
        "Connection failure should give an actionable Ukrainian database/connection message.");

    private static void AssertEveryOpenedResourceReleased(Fixture fixture)
    {
        if (fixture.Transactions.BeginCount != 0)
            AssertReleased(fixture);
    }

    private static void AssertReleased(Fixture fixture)
    {
        Assert.Equal(1, fixture.Transaction.DisposeCount);
        Assert.True(fixture.Transaction.DatabaseTransaction.IsDisposed);
        Assert.False(fixture.Transaction.IsActive);
    }

    private sealed class Fixture
    {
        public FakeState State { get; } = new();
        public FakeStockTransaction Transaction { get; }
        public FakeTransactionFactory Transactions { get; }
        public FakeStockRepository Stocks { get; }
        public FakeMovementRepository Movements { get; }
        public StockMovementService Service { get; }

        public Fixture(UserRole role = UserRole.Employee)
        {
            Transaction = new FakeStockTransaction(State);
            Transactions = new FakeTransactionFactory(Transaction);
            Stocks = new FakeStockRepository(State, Transaction);
            Movements = new FakeMovementRepository(Transaction);
            Service = new StockMovementService(new FakeCurrentUserContext(role), Stocks, Movements, Transactions);
        }
    }

    private sealed class FakeCurrentUserContext(UserRole role) : ICurrentUserContext
    {
        public User CurrentUser { get; } = new() { Id = 7, Role = role, IsActive = true };
        public int UserId => CurrentUser.Id;
        public UserRole Role => CurrentUser.Role;
        public bool IsAdmin => Role == UserRole.Admin;
    }

    private sealed class FakeState
    {
        public Stock PersistedStock { get; set; } = new()
        {
            ProductId = 11, WarehouseId = 22, Quantity = 10, MinThreshold = 3
        };
        public List<StockMovement> PersistedMovements { get; } = [];
    }

    private sealed class FakeTransactionFactory(FakeStockTransaction transaction) : IStockTransactionFactory
    {
        public int BeginCount { get; private set; }
        public Exception? BeginError { get; set; }

        public IStockTransaction Begin()
        {
            BeginCount++;
            if (BeginError is not null) throw BeginError;
            return transaction;
        }
    }

    private sealed class FakeStockTransaction(FakeState state) : IStockTransaction
    {
        public FakeDatabaseTransaction DatabaseTransaction { get; } = new();
        public IDbTransaction Transaction => DatabaseTransaction;
        public bool IsActive { get; private set; } = true;
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int DisposeCount { get; private set; }
        public Exception? CommitError { get; set; }
        public Exception? RollbackError { get; set; }
        public Stock? PendingStock { get; set; }
        public List<StockMovement> PendingMovements { get; } = [];

        public void Commit()
        {
            CommitCount++;
            if (CommitError is not null) throw CommitError;
            DatabaseTransaction.Commit();
            if (PendingStock is not null) state.PersistedStock = Copy(PendingStock);
            state.PersistedMovements.AddRange(PendingMovements.Select(Copy));
            PendingStock = null;
            PendingMovements.Clear();
            IsActive = false;
        }

        public void Rollback()
        {
            RollbackCount++;
            PendingStock = null;
            PendingMovements.Clear();
            DatabaseTransaction.Rollback();
            IsActive = false;
            if (RollbackError is not null) throw RollbackError;
        }

        public void Dispose()
        {
            DisposeCount++;
            PendingStock = null;
            PendingMovements.Clear();
            IsActive = false;
            DatabaseTransaction.Dispose();
        }
    }

    private sealed class FakeDatabaseTransaction : IDbTransaction
    {
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.Serializable;
        public bool IsDisposed { get; private set; }
        public void Commit() { }
        public void Rollback() { }
        public void Dispose() => IsDisposed = true;
    }

    private sealed class FakeStockRepository(FakeState state, FakeStockTransaction work) : IStockRepository
    {
        public int ReadCount { get; private set; }
        public int SaveCount { get; private set; }
        public (int, int) LastRequestedPair { get; private set; }
        public IDbTransaction? ReadTransaction { get; private set; }
        public IDbTransaction? SaveTransaction { get; private set; }
        public Exception? ReadError { get; set; }
        public Exception? SaveError { get; set; }

        public Stock GetForUpdate(int productId, int warehouseId, IDbTransaction transaction)
        {
            ReadCount++;
            LastRequestedPair = (productId, warehouseId);
            ReadTransaction = transaction;
            if (ReadError is not null) throw ReadError;
            return Copy(state.PersistedStock);
        }

        public void Save(Stock stock, IDbTransaction transaction)
        {
            SaveCount++;
            SaveTransaction = transaction;
            if (SaveError is not null) throw SaveError;
            work.PendingStock = Copy(stock);
        }

        public IReadOnlyList<Stock> GetAll(int? warehouseId) =>
            warehouseId is null || warehouseId == state.PersistedStock.WarehouseId
                ? [Copy(state.PersistedStock)] : [];
    }

    private sealed class FakeMovementRepository(FakeStockTransaction work) : IStockMovementRepository
    {
        public int AddCount { get; private set; }
        public IDbTransaction? AddTransaction { get; private set; }
        public Exception? AddError { get; set; }

        public int Add(StockMovement movement, IDbTransaction transaction)
        {
            AddCount++;
            AddTransaction = transaction;
            if (AddError is not null) throw AddError;
            work.PendingMovements.Add(Copy(movement));
            return 42;
        }
    }

    private static Stock Copy(Stock stock) => new()
    {
        ProductId = stock.ProductId, WarehouseId = stock.WarehouseId,
        Quantity = stock.Quantity, MinThreshold = stock.MinThreshold
    };

    private static StockMovement Copy(StockMovement movement) => new()
    {
        Id = movement.Id, Number = movement.Number, Type = movement.Type,
        ProductId = movement.ProductId, WarehouseId = movement.WarehouseId,
        SupplierId = movement.SupplierId, UserId = movement.UserId,
        Quantity = movement.Quantity, MovementDate = movement.MovementDate, Note = movement.Note
    };
}
