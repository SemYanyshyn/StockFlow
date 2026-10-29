using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class SupplierTests
{
    private static Supplier Valid() => new()
    {
        Name = "ТОВ «Молочна ферма»",
        Phone = "+380 44 000 00 01",
        Email = "Sales@Dairy.example"
    };

    [Fact]
    public void ValidSupplierIsAccepted() => Assert.True(Valid().Validate());

    [Fact]
    public void PhoneAndEmailAreOptional() =>
        Assert.True(new Supplier { Name = "ФОП Овочевий двір" }.Validate());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingNameIsRejected(string? name)
    {
        var supplier = Valid();
        supplier.Name = name!;
        Assert.False(supplier.Validate());
    }

    [Fact]
    public void TooLongNameIsRejected()
    {
        var supplier = Valid();
        supplier.Name = new string('п', Supplier.MaxNameLength + 1);
        Assert.False(supplier.Validate());
    }

    [Fact]
    public void TooLongPhoneIsRejected()
    {
        var supplier = Valid();
        supplier.Phone = new string('1', Supplier.MaxPhoneLength + 1);
        Assert.False(supplier.Validate());
    }

    [Theory]
    [InlineData("sales")]
    [InlineData("@dairy.example")]
    [InlineData("sales@")]
    [InlineData("sa@les@dairy.example")]
    [InlineData("sales @dairy.example")]
    public void InvalidEmailIsRejected(string email)
    {
        var supplier = Valid();
        supplier.Email = email;
        Assert.False(supplier.Validate());
    }

    [Fact]
    public void EmailAtMaxLengthIsAccepted()
    {
        const string domain = "@x.example";
        var supplier = Valid();
        supplier.Email = new string('a', Supplier.MaxEmailLength - domain.Length) + domain;
        Assert.True(supplier.Validate());
    }

    [Fact]
    public void TooLongEmailIsRejected()
    {
        const string domain = "@x.example";
        var supplier = Valid();
        supplier.Email = new string('a', Supplier.MaxEmailLength - domain.Length + 1) + domain;
        Assert.False(supplier.Validate());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyQueryMatchesEverySupplier(string? query) =>
        Assert.True(Valid().MatchesSearch(query));

    [Theory]
    [InlineData("молочна")]
    [InlineData("ФЕРМА")]
    [InlineData("000 00 01")]
    [InlineData("sales@dairy")]
    [InlineData("  DAIRY  ")]
    public void QueryMatchesNamePhoneOrEmail(string query) =>
        Assert.True(Valid().MatchesSearch(query));

    [Fact]
    public void NonMatchingQueryIsNotFound() =>
        Assert.False(Valid().MatchesSearch("бакалія"));

    [Fact]
    public void MissingPhoneAndEmailDoNotBreakSearch()
    {
        var supplier = new Supplier { Name = "ФОП Овочевий двір" };
        Assert.True(supplier.MatchesSearch("овочевий"));
        Assert.False(supplier.MatchesSearch("@"));
    }
}
