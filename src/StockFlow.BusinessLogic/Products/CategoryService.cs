using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess.Products;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Products;

public sealed class CategoryService
{
    public const string AdminOnlyMessage = "Додавати категорії може лише Адміністратор.";
    public const string ManageAdminOnlyMessage = "Змінювати й видаляти категорії може лише Адміністратор.";
    public const string NameRequiredMessage = "Назва категорії обов'язкова.";
    public const string DuplicateNameMessage = "Категорія з такою назвою вже існує.";
    public const string NotFoundMessage = "Категорію не знайдено. Оновіть список.";
    public const string InUseMessage = "Категорію не можна видалити, бо в ній є товари.";
    public static readonly string NameTooLongMessage =
        $"Назва категорії не може бути довшою за {Category.MaxNameLength} символів.";

    private readonly ICategoryRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public CategoryService(ICategoryRepository repository, ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(currentUser);
        _repository = repository;
        _currentUser = currentUser;
    }

    public IReadOnlyList<Category> GetAll() => _repository.GetAll();

    public Category Add(string? name)
    {
        if (!_currentUser.IsAdmin)
        {
            throw new ValidationException(AdminOnlyMessage);
        }

        var category = CreateValidCategory(0, name);

        try
        {
            category.Id = _repository.Add(category);
        }
        catch (DuplicateNameException)
        {
            throw new ValidationException(DuplicateNameMessage);
        }

        return category;
    }

    public Category Update(int id, string? name)
    {
        if (!_currentUser.IsAdmin)
        {
            throw new ValidationException(ManageAdminOnlyMessage);
        }

        var category = CreateValidCategory(id, name);

        try
        {
            if (!_repository.Update(category))
            {
                throw new ValidationException(NotFoundMessage);
            }
        }
        catch (DuplicateNameException)
        {
            throw new ValidationException(DuplicateNameMessage);
        }

        return category;
    }

    public void Delete(int id)
    {
        if (!_currentUser.IsAdmin)
        {
            throw new ValidationException(ManageAdminOnlyMessage);
        }

        var productCount = _repository.CountProducts(id);
        if (productCount > 0)
        {
            throw new ValidationException(
                $"Категорію не можна видалити: у ній {productCount} {ProductsWord(productCount)}.");
        }

        try
        {
            if (!_repository.Delete(id))
            {
                throw new ValidationException(NotFoundMessage);
            }
        }
        catch (RecordInUseException)
        {
            throw new ValidationException(InUseMessage);
        }
    }

    private Category CreateValidCategory(int id, string? name)
    {
        var category = new Category { Id = id, Name = name?.Trim() ?? string.Empty };
        if (!category.Validate())
        {
            throw new ValidationException(category.Name.Length == 0 ? NameRequiredMessage : NameTooLongMessage);
        }

        if (_repository.GetAll().Any(existing => existing.Id != id && IsSameName(existing.Name, category.Name)))
        {
            throw new ValidationException(DuplicateNameMessage);
        }

        return category;
    }

    private static bool IsSameName(string existing, string candidate) =>
        string.Equals(existing.Trim(), candidate, StringComparison.OrdinalIgnoreCase);

    private static string ProductsWord(int count) => (count % 10, count % 100) switch
    {
        (1, not 11) => "товар",
        (2 or 3 or 4, not (12 or 13 or 14)) => "товари",
        _ => "товарів"
    };
}
