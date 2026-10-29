using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class ProductTests
{
    private static Product Valid() => new() { Name = "Молоко", Unit = UnitOfMeasure.Liter, CategoryId = 1 };

    [Fact]
    public void ValidProductIsAccepted() => Assert.True(Valid().Validate());

    [Theory]
    [InlineData(UnitOfMeasure.Piece)]
    [InlineData(UnitOfMeasure.Kilogram)]
    [InlineData(UnitOfMeasure.Liter)]
    public void EveryDefinedUnitIsAccepted(UnitOfMeasure unit)
    {
        var product = Valid();
        product.Unit = unit;
        Assert.True(product.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingNameIsRejected(string? name)
    {
        var product = Valid();
        product.Name = name!;
        Assert.False(product.Validate());
    }

    [Fact]
    public void TooLongNameIsRejected()
    {
        var product = Valid();
        product.Name = new string('м', Product.MaxNameLength + 1);
        Assert.False(product.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MissingCategoryIsRejected(int categoryId)
    {
        var product = Valid();
        product.CategoryId = categoryId;
        Assert.False(product.Validate());
    }

    [Fact]
    public void UnknownUnitIsRejected()
    {
        var product = Valid();
        product.Unit = (UnitOfMeasure)99;
        Assert.False(product.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyQueryMatchesEveryProduct(string? query) =>
        Assert.True(Valid().MatchesSearch(query));

    [Theory]
    [InlineData("Молоко")]
    [InlineData("мол")]
    [InlineData("МОЛОКО")]
    [InlineData("око")]
    [InlineData("  мол  ")]
    public void MatchingQueryIsFound(string query) =>
        Assert.True(Valid().MatchesSearch(query));

    [Theory]
    [InlineData("хліб")]
    [InlineData("молоко 2")]
    public void NonMatchingQueryIsNotFound(string query) =>
        Assert.False(Valid().MatchesSearch(query));

    [Fact]
    public void SearchIgnoresCaseOfUkrainianLetters()
    {
        var product = Valid();
        product.Name = "Ґудзики їжачок";
        Assert.True(product.MatchesSearch("ґудзики ЇЖАЧОК"));
    }
}
