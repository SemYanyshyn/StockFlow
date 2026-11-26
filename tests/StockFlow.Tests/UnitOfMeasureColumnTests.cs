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

    [Theory]
    [InlineData(UnitOfMeasure.Piece, "piece")]
    [InlineData(UnitOfMeasure.Kilogram, "kg")]
    [InlineData(UnitOfMeasure.Liter, "liter")]
    public void UnitIsFormattedForDatabase(UnitOfMeasure unit, string expected) =>
        Assert.Equal(expected, UnitOfMeasureColumn.Format(unit));

    [Theory]
    [InlineData(UnitOfMeasure.Piece)]
    [InlineData(UnitOfMeasure.Kilogram)]
    [InlineData(UnitOfMeasure.Liter)]
    public void FormatAndParseRoundTrip(UnitOfMeasure unit) =>
        Assert.Equal(unit, UnitOfMeasureColumn.Parse(UnitOfMeasureColumn.Format(unit)));

    [Fact]
    public void UnknownUnitCannotBeFormatted() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => UnitOfMeasureColumn.Format((UnitOfMeasure)99));
}
