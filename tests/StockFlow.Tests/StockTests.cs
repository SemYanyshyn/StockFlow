using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public class StockTests
{
    [Theory]
    [InlineData(4, 10, true)]   
    [InlineData(10, 10, false)]
    [InlineData(15, 10, false)] 
    [InlineData(0, 0, false)]   
    public void IsBelowThreshold_EvaluatesCorrectly(int quantity, int threshold, bool expected)
    {
        var stock = new Stock { Quantity = quantity, MinThreshold = threshold };
        Assert.Equal(expected, stock.IsLowStock());
    }

    [Fact]
    public void Validate_ValidStock_ReturnsTrue()
    {
        var stock = new Stock { ProductId = 1, WarehouseId = 1, Quantity = 5, MinThreshold = 2 };
        Assert.True(stock.Validate());
    }
}