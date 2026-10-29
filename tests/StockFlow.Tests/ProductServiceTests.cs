using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess.Products;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class ProductServiceTests
{
    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = new();
        public int AddCalls { get; private set; }

        public IReadOnlyList<Product> GetAll() => Products.ToList();

        public int Add(Product product)
        {
            AddCalls++;
            var id = Products.Count + 1;
            Products.Add(new Product { Id = id, Name = product.Name, Unit = product.Unit, CategoryId = product.CategoryId });
            return id;
        }
    }

    private static (ProductService Service, FakeProductRepository Repository) Create(UserRole role = UserRole.Admin)
    {
        var repository = new FakeProductRepository();
        return (new ProductService(repository, new TemporaryUserContext(role)), repository);
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
        Assert.Throws<ArgumentNullException>(() =>
            new ProductService(null!, new TemporaryUserContext(UserRole.Admin)));

    [Fact]
    public void NullUserContextIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new ProductService(new FakeProductRepository(), null!));

    [Fact]
    public void GetAllReturnsRepositoryProducts()
    {
        var (service, repository) = Create(UserRole.Employee);
        repository.Products.AddRange(Products);
        Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(service.GetAll()));
    }

    [Fact]
    public void AdminAddsProductWithTrimmedName()
    {
        var (service, repository) = Create();
        var product = service.Add("  Сир кисломолочний  ", 1, UnitOfMeasure.Kilogram);
        Assert.Equal(1, product.Id);
        Assert.Equal("Сир кисломолочний", product.Name);
        Assert.Equal(1, product.CategoryId);
        Assert.Equal(UnitOfMeasure.Kilogram, product.Unit);
        Assert.Equal("Сир кисломолочний", Assert.Single(repository.Products).Name);
    }

    [Fact]
    public void EmployeeCannotAddProduct()
    {
        var (service, repository) = Create(UserRole.Employee);
        var error = Assert.Throws<ValidationException>(() => service.Add("Сир", 1, UnitOfMeasure.Kilogram));
        Assert.Equal(ProductService.AdminOnlyMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingNameIsRejectedWithoutSaving(string? name)
    {
        var (service, repository) = Create();
        var error = Assert.Throws<ValidationException>(() => service.Add(name, 1, UnitOfMeasure.Piece));
        Assert.Equal(ProductService.NameRequiredMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Fact]
    public void TooLongNameIsRejectedWithoutSaving()
    {
        var (service, repository) = Create();
        var error = Assert.Throws<ValidationException>(() =>
            service.Add(new string('т', Product.MaxNameLength + 1), 1, UnitOfMeasure.Piece));
        Assert.Equal(ProductService.NameTooLongMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void MissingCategoryIsRejectedWithoutSaving(int? categoryId)
    {
        var (service, repository) = Create();
        var error = Assert.Throws<ValidationException>(() => service.Add("Сир", categoryId, UnitOfMeasure.Piece));
        Assert.Equal(ProductService.CategoryRequiredMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((UnitOfMeasure)99)]
    public void MissingOrUnknownUnitIsRejectedWithoutSaving(UnitOfMeasure? unit)
    {
        var (service, repository) = Create();
        var error = Assert.Throws<ValidationException>(() => service.Add("Сир", 1, unit));
        Assert.Equal(ProductService.UnitRequiredMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Fact]
    public void DuplicateProductNamesAreAllowed()
    {
        var (service, repository) = Create();
        service.Add("Молоко", 1, UnitOfMeasure.Liter);
        service.Add("Молоко", 2, UnitOfMeasure.Liter);
        Assert.Equal(2, repository.AddCalls);
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
