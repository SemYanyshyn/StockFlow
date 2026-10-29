using StockFlow.BusinessLogic.Products;
using StockFlow.DataAccess.Products;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class ProductServiceTests
{
    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = new();

        public IReadOnlyList<Product> GetAll() => Products.ToList();
    }

    private static readonly Product[] Products =
    {
        new() { Id = 1, Name = "Молоко", Unit = UnitOfMeasure.Liter, CategoryId = 1 },
        new() { Id = 2, Name = "Кефір", Unit = UnitOfMeasure.Liter, CategoryId = 1 },
        new() { Id = 3, Name = "Яблука", Unit = UnitOfMeasure.Kilogram, CategoryId = 2 },
        new() { Id = 4, Name = "Молоток", Unit = UnitOfMeasure.Piece, CategoryId = 3 }
    };

    private static int[] Ids(IEnumerable<Product> products) => products.Select(p => p.Id).ToArray();

    [Fact]
    public void NullRepositoryIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new ProductService(null!));

    [Fact]
    public void GetAllReturnsRepositoryProducts()
    {
        var repository = new FakeProductRepository();
        repository.Products.AddRange(Products);
        Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(new ProductService(repository).GetAll()));
    }

    [Fact]
    public void NoFilterReturnsEveryProduct() =>
        Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(ProductService.Filter(Products, null, null)));

    [Fact]
    public void CategoryFilterNarrowsList() =>
        Assert.Equal(new[] { 1, 2 }, Ids(ProductService.Filter(Products, 1, null)));

    [Fact]
    public void SearchNarrowsList() =>
        Assert.Equal(new[] { 1, 4 }, Ids(ProductService.Filter(Products, null, "мол")));

    [Fact]
    public void CategoryAndSearchAreCombined() =>
        Assert.Equal(new[] { 1 }, Ids(ProductService.Filter(Products, 1, "мол")));

    [Fact]
    public void NoMatchesReturnsEmptyList()
    {
        Assert.Empty(ProductService.Filter(Products, 2, "мол"));
        Assert.Empty(ProductService.Filter(Products, 99, null));
    }

    [Fact]
    public void FilterDoesNotChangeSource()
    {
        var source = Products.ToList();
        ProductService.Filter(source, 1, "мол");
        Assert.Equal(4, source.Count);
    }

    [Fact]
    public void NullProductsAreRejected() =>
        Assert.Throws<ArgumentNullException>(() => ProductService.Filter(null!, null, null));
}
