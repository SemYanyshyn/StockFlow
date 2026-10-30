using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class StockMovementTests
{
    [Theory]
    [InlineData(0, 7, 7)]
    [InlineData(0, int.MaxValue, int.MaxValue)]
    [InlineData(11, 7, 18)]
    [InlineData(int.MaxValue - 1, 1, int.MaxValue)]
    public void ReceiptIncreasesOnlyTheMatchingStockQuantity(int before, int amount, int expected)
    {
        var stock = Stock(before);
        var movement = Movement(MovementType.Receipt, amount);

        Assert.True(movement.Validate(stock));
        Assert.Null(movement.GetValidationError(stock));
        movement.Apply(stock);

        Assert.Equal(expected, stock.Quantity);
        Assert.Equal(5, stock.MinThreshold);
        Assert.Equal(12, stock.ProductId);
        Assert.Equal(3, stock.WarehouseId);
    }

    [Theory]
    [InlineData(10, 3, 7)]
    [InlineData(10, 10, 0)]
    [InlineData(1, 1, 0)]
    public void WriteOffSupportsPartialAndWholeStock(int before, int amount, int expected)
    {
        var stock = Stock(before);
        var movement = Movement(MovementType.WriteOff, amount);

        Assert.True(movement.Validate(stock));
        movement.Apply(stock);

        Assert.Equal(expected, stock.Quantity);
        Assert.Equal(5, stock.MinThreshold);
    }

    [Theory]
    [InlineData(MovementType.Receipt)]
    [InlineData(MovementType.WriteOff)]
    public void ValidateNeverChangesStock(MovementType type)
    {
        var stock = Stock(10);
        var movement = Movement(type, 3);
        var before = Snapshot(stock);

        Assert.True(movement.Validate(stock));
        Assert.True(movement.Validate(stock));

        Assert.Equal(before, Snapshot(stock));
    }

    [Theory]
    [InlineData("null-number", "Номер накладної обов’язковий")]
    [InlineData("empty-number", "Номер накладної обов’язковий")]
    [InlineData("blank-number", "Номер накладної обов’язковий")]
    [InlineData("long-number", "Номер накладної не може перевищувати 50 символів")]
    [InlineData("zero-quantity", "Кількість має бути більшою за 0")]
    [InlineData("negative-quantity", "Кількість має бути більшою за 0")]
    [InlineData("unknown-type", "Невідомий тип руху товару")]
    [InlineData("negative-id", "Некоректні ідентифікатори руху товару")]
    [InlineData("zero-product", "Некоректні ідентифікатори руху товару")]
    [InlineData("negative-product", "Некоректні ідентифікатори руху товару")]
    [InlineData("zero-warehouse", "Некоректні ідентифікатори руху товару")]
    [InlineData("negative-warehouse", "Некоректні ідентифікатори руху товару")]
    [InlineData("zero-user", "Некоректні ідентифікатори руху товару")]
    [InlineData("negative-user", "Некоректні ідентифікатори руху товару")]
    [InlineData("missing-date", "Потрібно вказати дату руху товару")]
    [InlineData("receipt-no-supplier", "Для надходження потрібно обрати постачальника")]
    [InlineData("receipt-zero-supplier", "Для надходження потрібно обрати постачальника")]
    [InlineData("receipt-negative-supplier", "Для надходження потрібно обрати постачальника")]
    [InlineData("write-off-with-supplier", "Для списання постачальник не зазначається")]
    [InlineData("write-off-with-zero-supplier", "Для списання постачальник не зазначається")]
    [InlineData("different-product", "Залишок не відповідає обраному товару та складу")]
    [InlineData("different-warehouse", "Залишок не відповідає обраному товару та складу")]
    [InlineData("invalid-stock-product", "Некоректний залишок товару на складі")]
    [InlineData("invalid-stock-warehouse", "Некоректний залишок товару на складі")]
    [InlineData("negative-stock", "Некоректний залишок товару на складі")]
    [InlineData("overdraw", "Недостатньо товару на складі")]
    [InlineData("overflow", "Кількість товару перевищує допустиме значення")]
    public void RejectedMovementProvidesOneErrorAndCannotChangeStock(string invalidCase, string expectedError)
    {
        var stock = Stock(10);
        var movement = Movement(MovementType.Receipt, 3);
        MakeInvalid(invalidCase, movement, stock);
        var before = Snapshot(stock);

        Assert.False(movement.Validate(stock));
        Assert.Equal(expectedError, movement.GetValidationError(stock));
        Assert.Equal(before, Snapshot(stock));

        var error = Assert.Throws<StockMovementValidationException>(() => movement.Apply(stock));
        Assert.Equal(expectedError, error.Message);
        Assert.Equal(before, Snapshot(stock));
    }

    [Fact]
    public void MissingStockIsRejectedWithoutNullReferenceFailure()
    {
        var movement = Movement(MovementType.Receipt, 3);

        Assert.False(movement.Validate(null!));
        Assert.Equal("Не отримано залишок товару на складі", movement.GetValidationError(null));
        var error = Assert.Throws<StockMovementValidationException>(() => movement.Apply(null!));
        Assert.Equal(movement.GetValidationError(null), error.Message);
    }

    [Fact]
    public void NumberWithExactlyTheSchemaLimitIsAccepted()
    {
        var movement = Movement(MovementType.Receipt, 1);
        movement.Number = new string('Н', 50);

        Assert.True(movement.Validate(Stock(0)));
    }

    [Fact]
    public void WhitespaceStillCountsTowardTheStoredNumberLength()
    {
        var movement = Movement(MovementType.Receipt, 1);
        movement.Number = " " + new string('Н', 49) + " ";

        Assert.False(movement.Validate(Stock(0)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    public void UnsavedAndPersistedMovementIdentifiersAreAccepted(int id)
    {
        var movement = Movement(MovementType.Receipt, 1);
        movement.Id = id;

        Assert.True(movement.Validate(Stock(0)));
    }

    [Fact]
    public void ApplyDoesNotChangeMinimumThresholdEvenForTheFirstReceipt()
    {
        var stock = Stock(0);
        stock.MinThreshold = 0;

        Movement(MovementType.Receipt, 3).Apply(stock);

        Assert.Equal(3, stock.Quantity);
        Assert.Equal(0, stock.MinThreshold);
    }

    private static Stock Stock(int quantity) => new()
    {
        ProductId = 12,
        WarehouseId = 3,
        Quantity = quantity,
        MinThreshold = 5
    };

    private static StockMovement Movement(MovementType type, int quantity) => new()
    {
        Number = "НК-017",
        Type = type,
        ProductId = 12,
        WarehouseId = 3,
        SupplierId = type == MovementType.Receipt ? 8 : null,
        UserId = 2,
        Quantity = quantity,
        MovementDate = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Unspecified)
    };

    private static (int, int, int, int) Snapshot(Stock stock) =>
        (stock.ProductId, stock.WarehouseId, stock.Quantity, stock.MinThreshold);

    private static void MakeInvalid(string invalidCase, StockMovement movement, Stock stock)
    {
        switch (invalidCase)
        {
            case "null-number": movement.Number = null!; break;
            case "empty-number": movement.Number = ""; break;
            case "blank-number": movement.Number = " \t "; break;
            case "long-number": movement.Number = new string('Н', 51); break;
            case "zero-quantity": movement.Quantity = 0; break;
            case "negative-quantity": movement.Quantity = -1; break;
            case "unknown-type": movement.Type = (MovementType)999; break;
            case "negative-id": movement.Id = -1; break;
            case "zero-product": movement.ProductId = 0; break;
            case "negative-product": movement.ProductId = -1; break;
            case "zero-warehouse": movement.WarehouseId = 0; break;
            case "negative-warehouse": movement.WarehouseId = -1; break;
            case "zero-user": movement.UserId = 0; break;
            case "negative-user": movement.UserId = -1; break;
            case "missing-date": movement.MovementDate = default; break;
            case "receipt-no-supplier": movement.SupplierId = null; break;
            case "receipt-zero-supplier": movement.SupplierId = 0; break;
            case "receipt-negative-supplier": movement.SupplierId = -1; break;
            case "write-off-with-supplier": movement.Type = MovementType.WriteOff; break;
            case "write-off-with-zero-supplier": movement.Type = MovementType.WriteOff; movement.SupplierId = 0; break;
            case "different-product": stock.ProductId++; break;
            case "different-warehouse": stock.WarehouseId++; break;
            case "invalid-stock-product": stock.ProductId = 0; break;
            case "invalid-stock-warehouse": stock.WarehouseId = 0; break;
            case "negative-stock": stock.Quantity = -1; break;
            case "overdraw": movement.Type = MovementType.WriteOff; movement.SupplierId = null; movement.Quantity = 11; break;
            case "overflow": stock.Quantity = int.MaxValue; break;
            default: throw new ArgumentOutOfRangeException(nameof(invalidCase));
        }
    }
}
