using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public class WarehouseTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_InvalidName_ReturnsFalse(string? name)
    {
        var warehouse = new Warehouse { Id = 1, Name = name! };
        Assert.False(warehouse.Validate());
    }

    [Fact]
    public void Validate_ValidWarehouse_ReturnsTrue()
    {
        var warehouse = new Warehouse { Id = 1, Name = "Основний склад", Address = "м. Львів" };
        Assert.True(warehouse.Validate());
    }
}