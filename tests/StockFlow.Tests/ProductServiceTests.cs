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
        public HashSet<int> ProductsWithRecords { get; } = new();
        public bool ThrowInUseOnDelete { get; set; }
        public int AddCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public IReadOnlyList<Product> GetAll() => Products.ToList();

        public int Add(Product product)
        {
            AddCalls++;
            var id = Products.Count + 1;
            Products.Add(new Product { Id = id, Name = product.Name, Unit = product.Unit, CategoryId = product.CategoryId });
            return id;
        }

        public Product? GetById(int productId) => Products.FirstOrDefault(p => p.Id == productId) is { } p
            ? new Product { Id = p.Id, Name = p.Name, Unit = p.Unit, CategoryId = p.CategoryId }
            : null;

        public bool Update(Product product)
        {
            UpdateCalls++;
            var existing = Products.FirstOrDefault(p => p.Id == product.Id);
            if (existing is null)
            {
                return false;
            }

            existing.Name = product.Name;
            existing.Unit = product.Unit;
            existing.CategoryId = product.CategoryId;
            return true;
        }

        public bool HasRelatedRecords(int productId) => ProductsWithRecords.Contains(productId);

        public bool Delete(int productId)
        {
            DeleteCalls++;
            if (ThrowInUseOnDelete)
            {
                throw new RecordInUseException();
            }

            return Products.RemoveAll(p => p.Id == productId) == 1;
        }
    }

    private static (ProductService Service, FakeProductRepository Repository) CreateWithMilk(UserRole role = UserRole.Admin)
    {
        var (service, repository) = Create(role);
        repository.Products.Add(new Product { Id = 1, Name = "Молоко", Unit = UnitOfMeasure.Liter, CategoryId = 1 });
        return (service, repository);
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

    [Fact]
    public void AdminEditsProductWithTrimmedName()
    {
        var (service, repository) = CreateWithMilk();
        var product = service.Update(1, "  Молоко 3,2%  ", 2, UnitOfMeasure.Piece);
        Assert.Equal("Молоко 3,2%", product.Name);
        var stored = Assert.Single(repository.Products);
        Assert.Equal(("Молоко 3,2%", 2, UnitOfMeasure.Piece), (stored.Name, stored.CategoryId, stored.Unit));
    }

    [Fact]
    public void EmployeeCannotEditProduct()
    {
        var (service, repository) = CreateWithMilk(UserRole.Employee);
        var error = Assert.Throws<ValidationException>(() => service.Update(1, "Сир", 1, UnitOfMeasure.Liter));
        Assert.Equal(ProductService.ManageAdminOnlyMessage, error.Message);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Theory]
    [InlineData("   ", 1, UnitOfMeasure.Liter, ProductService.NameRequiredMessage)]
    [InlineData("Молоко", 0, UnitOfMeasure.Liter, ProductService.CategoryRequiredMessage)]
    [InlineData("Молоко", 1, (UnitOfMeasure)99, ProductService.UnitRequiredMessage)]
    public void EditUsesTheSameValidationAsAdd(string name, int categoryId, UnitOfMeasure unit, string expected)
    {
        var (service, repository) = CreateWithMilk();
        var error = Assert.Throws<ValidationException>(() => service.Update(1, name, categoryId, unit));
        Assert.Equal(expected, error.Message);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public void UnitCannotChangeWhenProductHasStockOrMovements()
    {
        var (service, repository) = CreateWithMilk();
        repository.ProductsWithRecords.Add(1);
        var error = Assert.Throws<ValidationException>(() => service.Update(1, "Молоко", 1, UnitOfMeasure.Piece));
        Assert.Equal(ProductService.UnitLockedMessage, error.Message);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public void NameAndCategoryCanChangeWhenProductHasStockOrMovements()
    {
        var (service, repository) = CreateWithMilk();
        repository.ProductsWithRecords.Add(1);
        service.Update(1, "Молоко пастеризоване", 2, UnitOfMeasure.Liter);
        Assert.Equal("Молоко пастеризоване", Assert.Single(repository.Products).Name);
    }

    [Fact]
    public void EditOfMissingProductReportsNotFound()
    {
        var (service, _) = CreateWithMilk();
        var error = Assert.Throws<ValidationException>(() => service.Update(99, "Сир", 1, UnitOfMeasure.Kilogram));
        Assert.Equal(ProductService.NotFoundMessage, error.Message);
    }

    [Fact]
    public void AdminDeletesProductWithoutRecords()
    {
        var (service, repository) = CreateWithMilk();
        service.Delete(1);
        Assert.Empty(repository.Products);
    }

    [Fact]
    public void EmployeeCannotDeleteProduct()
    {
        var (service, repository) = CreateWithMilk(UserRole.Employee);
        var error = Assert.Throws<ValidationException>(() => service.Delete(1));
        Assert.Equal(ProductService.ManageAdminOnlyMessage, error.Message);
        Assert.Equal(0, repository.DeleteCalls);
    }

    [Fact]
    public void ProductWithStockOrMovementsIsNotDeleted()
    {
        var (service, repository) = CreateWithMilk();
        repository.ProductsWithRecords.Add(1);
        var error = Assert.Throws<ValidationException>(() => service.Delete(1));
        Assert.Equal(ProductService.InUseMessage, error.Message);
        Assert.Equal(0, repository.DeleteCalls);
        Assert.Single(repository.Products);
    }

    [Fact]
    public void DeleteOfMissingProductReportsNotFound()
    {
        var (service, _) = CreateWithMilk();
        var error = Assert.Throws<ValidationException>(() => service.Delete(99));
        Assert.Equal(ProductService.NotFoundMessage, error.Message);
    }

    [Fact]
    public void ForeignKeyViolationOnDeleteIsReportedAsValidationError()
    {
        var (service, repository) = CreateWithMilk();
        repository.ThrowInUseOnDelete = true;
        var error = Assert.Throws<ValidationException>(() => service.Delete(1));
        Assert.Equal(ProductService.InUseMessage, error.Message);
    }
}
