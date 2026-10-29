using StockFlow.DataAccess.Products;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class UnitOfMeasureColumnTests
{
    [Theory]
    [InlineData("piece", UnitOfMeasure.Piece)]
    [InlineData("kg", UnitOfMeasure.Kilogram)]
    [InlineData("liter", UnitOfMeasure.Liter)]
    public void DatabaseValueIsMapped(string value, UnitOfMeasure expected) =>
        Assert.Equal(expected, UnitOfMeasureColumn.Parse(value));

    [Theory]
    [InlineData("")]
    [InlineData("Piece")]
    [InlineData("kilogram")]
    [InlineData("0")]
    public void UnknownDatabaseValueIsRejected(string value) =>
        Assert.Throws<InvalidOperationException>(() => UnitOfMeasureColumn.Parse(value));
}
