using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class CategoryTests
{
    [Theory]
    [InlineData("Напої")]
    [InlineData("  Напої  ")]
    [InlineData("A")]
    public void ValidNameIsAccepted(string name) =>
        Assert.True(new Category { Name = name }.Validate());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingNameIsRejected(string? name) =>
        Assert.False(new Category { Name = name! }.Validate());

    [Fact]
    public void NameAtMaxLengthIsAccepted() =>
        Assert.True(new Category { Name = new string('к', Category.MaxNameLength) }.Validate());

    [Fact]
    public void TooLongNameIsRejected() =>
        Assert.False(new Category { Name = new string('к', Category.MaxNameLength + 1) }.Validate());

    [Fact]
    public void SurroundingSpacesDoNotCountTowardsMaxLength() =>
        Assert.True(new Category { Name = " " + new string('к', Category.MaxNameLength) + " " }.Validate());

    [Fact]
    public void ValidateDoesNotChangeName()
    {
        var category = new Category { Name = "  Напої  " };
        category.Validate();
        Assert.Equal("  Напої  ", category.Name);
    }
}
