using StockFlow.BusinessLogic.Products;
using StockFlow.DataAccess.Products;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class CategoryServiceTests
{
    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        public List<Category> Categories { get; } = new();
        public bool ThrowDuplicateOnAdd { get; set; }
        public int AddCalls { get; private set; }

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
    }

    private static (CategoryService Service, FakeCategoryRepository Repository) Create(params string[] existing)
    {
        var repository = new FakeCategoryRepository();
        foreach (var name in existing)
        {
            repository.Categories.Add(new Category { Id = repository.Categories.Count + 1, Name = name });
        }

        return (new CategoryService(repository), repository);
    }

    [Fact]
    public void NullRepositoryIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new CategoryService(null!));

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
}
