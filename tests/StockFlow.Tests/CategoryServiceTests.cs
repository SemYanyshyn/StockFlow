using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess.Products;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class CategoryServiceTests
{
    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        public List<Category> Categories { get; } = new();
        public Dictionary<int, int> ProductCounts { get; } = new();
        public bool ThrowDuplicateOnAdd { get; set; }
        public bool ThrowDuplicateOnUpdate { get; set; }
        public bool ThrowInUseOnDelete { get; set; }
        public int AddCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public IReadOnlyList<Category> GetAll() => Categories.ToList();

        public int Add(Category category)
        {
            AddCalls++;
            if (ThrowDuplicateOnAdd)
            {
                throw new DuplicateNameException();
            }

            var id = Categories.Count + 1;
            Categories.Add(new Category { Id = id, Name = category.Name });
            return id;
        }

        public bool Update(Category category)
        {
            UpdateCalls++;
            if (ThrowDuplicateOnUpdate)
            {
                throw new DuplicateNameException();
            }

            var existing = Categories.FirstOrDefault(c => c.Id == category.Id);
            if (existing is null)
            {
                return false;
            }

            existing.Name = category.Name;
            return true;
        }

        public int CountProducts(int categoryId) => ProductCounts.GetValueOrDefault(categoryId);

        public bool Delete(int categoryId)
        {
            DeleteCalls++;
            if (ThrowInUseOnDelete)
            {
                throw new RecordInUseException();
            }

            return Categories.RemoveAll(c => c.Id == categoryId) == 1;
        }
    }

    private static (CategoryService Service, FakeCategoryRepository Repository) Create(params string[] existing) =>
        CreateAs(UserRole.Admin, existing);

    private static (CategoryService Service, FakeCategoryRepository Repository) CreateAs(UserRole role, params string[] existing)
    {
        var repository = new FakeCategoryRepository();
        foreach (var name in existing)
        {
            repository.Categories.Add(new Category { Id = repository.Categories.Count + 1, Name = name });
        }

        return (new CategoryService(repository, new TemporaryUserContext(role)), repository);
    }

    [Fact]
    public void NullRepositoryIsRejected() =>
        Assert.Throws<ArgumentNullException>(() =>
            new CategoryService(null!, new TemporaryUserContext(UserRole.Admin)));

    [Fact]
    public void NullUserContextIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new CategoryService(new FakeCategoryRepository(), null!));

    [Fact]
    public void EmployeeCannotAddCategory()
    {
        var (service, repository) = CreateAs(UserRole.Employee);
        var error = Assert.Throws<ValidationException>(() => service.Add("Напої"));
        Assert.Equal(CategoryService.AdminOnlyMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Fact]
    public void EmployeeCanViewCategories()
    {
        var (service, _) = CreateAs(UserRole.Employee, "Напої");
        Assert.Single(service.GetAll());
    }

    [Fact]
    public void GetAllReturnsRepositoryCategories()
    {
        var (service, _) = Create("Напої", "Молочні продукти");
        Assert.Equal(new[] { "Напої", "Молочні продукти" }, service.GetAll().Select(c => c.Name));
    }

    [Fact]
    public void ValidCategoryIsAddedWithIdAndTrimmedName()
    {
        var (service, repository) = Create("Напої");
        var category = service.Add("  Овочі  ");
        Assert.Equal(2, category.Id);
        Assert.Equal("Овочі", category.Name);
        Assert.Equal("Овочі", repository.Categories[1].Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingNameIsRejectedWithoutSaving(string? name)
    {
        var (service, repository) = Create();
        var error = Assert.Throws<ValidationException>(() => service.Add(name));
        Assert.Equal(CategoryService.NameRequiredMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Fact]
    public void TooLongNameIsRejectedWithoutSaving()
    {
        var (service, repository) = Create();
        var error = Assert.Throws<ValidationException>(() => service.Add(new string('к', Category.MaxNameLength + 1)));
        Assert.Equal(CategoryService.NameTooLongMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Theory]
    [InlineData("Напої", "Напої")]
    [InlineData("Напої", "напої")]
    [InlineData("Напої", "НАПОЇ")]
    [InlineData("Напої", "  напої ")]
    [InlineData("Ґудзики", "ґудзики")]
    [InlineData("Їжа для тварин", "їЖА ДЛЯ ТВАРИН")]
    [InlineData("Drinks", "drinks")]
    public void DuplicateNameIsRejectedWithoutSaving(string existing, string candidate)
    {
        var (service, repository) = Create(existing);
        var error = Assert.Throws<ValidationException>(() => service.Add(candidate));
        Assert.Equal(CategoryService.DuplicateNameMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Fact]
    public void SimilarButDifferentNameIsAccepted()
    {
        var (service, _) = Create("Напої");
        Assert.Equal("Напої гарячі", service.Add("Напої гарячі").Name);
    }

    [Fact]
    public void DuplicateDetectedByDatabaseIsReportedAsValidationError()
    {
        var (service, repository) = Create();
        repository.ThrowDuplicateOnAdd = true;
        var error = Assert.Throws<ValidationException>(() => service.Add("Напої"));
        Assert.Equal(CategoryService.DuplicateNameMessage, error.Message);
    }

    [Fact]
    public void AdminRenamesCategoryWithTrimmedName()
    {
        var (service, repository) = Create("Напої", "Бакалія");
        var category = service.Update(1, "  Напої та соки  ");
        Assert.Equal("Напої та соки", category.Name);
        Assert.Equal("Напої та соки", repository.Categories[0].Name);
    }

    [Fact]
    public void CategoryCanChangeCaseOfItsOwnName()
    {
        var (service, repository) = Create("напої");
        service.Update(1, "Напої");
        Assert.Equal("Напої", repository.Categories[0].Name);
    }

    [Fact]
    public void EmployeeCannotRenameCategory()
    {
        var (service, repository) = CreateAs(UserRole.Employee, "Напої");
        var error = Assert.Throws<ValidationException>(() => service.Update(1, "Соки"));
        Assert.Equal(CategoryService.ManageAdminOnlyMessage, error.Message);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void RenameToMissingNameIsRejected(string? name)
    {
        var (service, repository) = Create("Напої");
        var error = Assert.Throws<ValidationException>(() => service.Update(1, name));
        Assert.Equal(CategoryService.NameRequiredMessage, error.Message);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public void RenameToTooLongNameIsRejected()
    {
        var (service, repository) = Create("Напої");
        var error = Assert.Throws<ValidationException>(() => service.Update(1, new string('к', Category.MaxNameLength + 1)));
        Assert.Equal(CategoryService.NameTooLongMessage, error.Message);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Theory]
    [InlineData("Бакалія")]
    [InlineData("БАКАЛІЯ")]
    [InlineData("  бакалія ")]
    public void RenameToOtherCategoryNameIsRejected(string name)
    {
        var (service, repository) = Create("Напої", "Бакалія");
        var error = Assert.Throws<ValidationException>(() => service.Update(1, name));
        Assert.Equal(CategoryService.DuplicateNameMessage, error.Message);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public void RenameOfMissingCategoryReportsNotFound()
    {
        var (service, _) = Create("Напої");
        var error = Assert.Throws<ValidationException>(() => service.Update(99, "Соки"));
        Assert.Equal(CategoryService.NotFoundMessage, error.Message);
    }

    [Fact]
    public void RenameDuplicateDetectedByDatabaseIsReportedAsValidationError()
    {
        var (service, repository) = Create("Напої");
        repository.ThrowDuplicateOnUpdate = true;
        var error = Assert.Throws<ValidationException>(() => service.Update(1, "Соки"));
        Assert.Equal(CategoryService.DuplicateNameMessage, error.Message);
    }

    [Fact]
    public void AdminDeletesEmptyCategory()
    {
        var (service, repository) = Create("Напої", "Бакалія");
        service.Delete(2);
        Assert.Equal("Напої", Assert.Single(repository.Categories).Name);
    }

    [Fact]
    public void EmployeeCannotDeleteCategory()
    {
        var (service, repository) = CreateAs(UserRole.Employee, "Напої");
        var error = Assert.Throws<ValidationException>(() => service.Delete(1));
        Assert.Equal(CategoryService.ManageAdminOnlyMessage, error.Message);
        Assert.Equal(0, repository.DeleteCalls);
    }

    [Theory]
    [InlineData(1, "Категорію не можна видалити: у ній 1 товар.")]
    [InlineData(3, "Категорію не можна видалити: у ній 3 товари.")]
    [InlineData(5, "Категорію не можна видалити: у ній 5 товарів.")]
    [InlineData(11, "Категорію не можна видалити: у ній 11 товарів.")]
    [InlineData(12, "Категорію не можна видалити: у ній 12 товарів.")]
    [InlineData(21, "Категорію не можна видалити: у ній 21 товар.")]
    [InlineData(22, "Категорію не можна видалити: у ній 22 товари.")]
    public void CategoryWithProductsIsNotDeletedAndCountIsShown(int count, string expected)
    {
        var (service, repository) = Create("Напої");
        repository.ProductCounts[1] = count;
        var error = Assert.Throws<ValidationException>(() => service.Delete(1));
        Assert.Equal(expected, error.Message);
        Assert.Equal(0, repository.DeleteCalls);
        Assert.Single(repository.Categories);
    }

    [Fact]
    public void DeleteOfMissingCategoryReportsNotFound()
    {
        var (service, _) = Create("Напої");
        var error = Assert.Throws<ValidationException>(() => service.Delete(99));
        Assert.Equal(CategoryService.NotFoundMessage, error.Message);
    }

    [Fact]
    public void ForeignKeyViolationIsReportedAsValidationError()
    {
        var (service, repository) = Create("Напої");
        repository.ThrowInUseOnDelete = true;
        var error = Assert.Throws<ValidationException>(() => service.Delete(1));
        Assert.Equal(CategoryService.InUseMessage, error.Message);
    }
}
