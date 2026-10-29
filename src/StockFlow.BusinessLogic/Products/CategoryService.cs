using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess.Products;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Products;

public sealed class CategoryService
{
    public const string AdminOnlyMessage = "Додавати категорії може лише Адміністратор.";
    public const string NameRequiredMessage = "Назва категорії обов'язкова.";
    public const string DuplicateNameMessage = "Категорія з такою назвою вже існує.";
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

        var category = new Category { Name = name?.Trim() ?? string.Empty };
        if (!category.Validate())
        {
            throw new ValidationException(category.Name.Length == 0 ? NameRequiredMessage : NameTooLongMessage);
        }

        if (_repository.GetAll().Any(existing => IsSameName(existing.Name, category.Name)))
        {
            throw new ValidationException(DuplicateNameMessage);
        }

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

    private static bool IsSameName(string existing, string candidate) =>
        string.Equals(existing.Trim(), candidate, StringComparison.OrdinalIgnoreCase);
}
