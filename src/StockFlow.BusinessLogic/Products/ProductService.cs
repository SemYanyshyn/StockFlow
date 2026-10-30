using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess.Products;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Products;

public sealed class ProductService
{
    public const string AdminOnlyMessage = "Додавати товари може лише Адміністратор.";
    public const string NameRequiredMessage = "Назва товару обов'язкова.";
    public const string CategoryRequiredMessage = "Оберіть категорію товару.";
    public const string UnitRequiredMessage = "Оберіть одиницю виміру.";
    public const string InvalidProductMessage = "Некоректні дані товару.";
    public const string ManageAdminOnlyMessage = "Змінювати й видаляти товари може лише Адміністратор.";
    public const string NotFoundMessage = "Товар не знайдено. Оновіть список.";
    public const string UnitLockedMessage = "Одиницю виміру не можна змінити: для товару вже є залишки або рухи.";
    public const string InUseMessage = "Товар не можна видалити: для нього є залишки або рухи товару.";
    public static readonly string NameTooLongMessage =
        $"Назва товару не може бути довшою за {Product.MaxNameLength} символів.";

    private readonly IProductRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public ProductService(IProductRepository repository, ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(currentUser);
        _repository = repository;
        _currentUser = currentUser;
    }

    public IReadOnlyList<Product> GetAll() => _repository.GetAll();

    public Product Add(string? name, int? categoryId, UnitOfMeasure? unit)
    {
        if (!_currentUser.IsAdmin)
        {
            throw new ValidationException(AdminOnlyMessage);
        }

        var product = CreateValidProduct(0, name, categoryId, unit);
        product.Id = _repository.Add(product);
        return product;
    }

    public Product Update(int id, string? name, int? categoryId, UnitOfMeasure? unit)
    {
        if (!_currentUser.IsAdmin)
        {
            throw new ValidationException(ManageAdminOnlyMessage);
        }

        var product = CreateValidProduct(id, name, categoryId, unit);
        var current = _repository.GetById(id) ?? throw new ValidationException(NotFoundMessage);
        if (current.Unit != product.Unit && _repository.HasRelatedRecords(id))
        {
            throw new ValidationException(UnitLockedMessage);
        }

        if (!_repository.Update(product))
        {
            throw new ValidationException(NotFoundMessage);
        }

        return product;
    }

    public void Delete(int id)
    {
        if (!_currentUser.IsAdmin)
        {
            throw new ValidationException(ManageAdminOnlyMessage);
        }

        if (_repository.HasRelatedRecords(id))
        {
            throw new ValidationException(InUseMessage);
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

    private static Product CreateValidProduct(int id, string? name, int? categoryId, UnitOfMeasure? unit)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
        {
            throw new ValidationException(NameRequiredMessage);
        }

        if (trimmedName.Length > Product.MaxNameLength)
        {
            throw new ValidationException(NameTooLongMessage);
        }

        if (categoryId is not > 0)
        {
            throw new ValidationException(CategoryRequiredMessage);
        }

        if (unit is null || !Enum.IsDefined(unit.Value))
        {
            throw new ValidationException(UnitRequiredMessage);
        }

        var product = new Product { Id = id, Name = trimmedName, CategoryId = categoryId.Value, Unit = unit.Value };
        if (!product.Validate())
        {
            throw new ValidationException(InvalidProductMessage);
        }

        return product;
    }

    public static IReadOnlyList<Product> Filter(IEnumerable<Product> products, int? categoryId, string? query)
    {
        ArgumentNullException.ThrowIfNull(products);
        return products
            .Where(product => (categoryId is null || product.CategoryId == categoryId) && product.MatchesSearch(query))
            .ToList();
    }
}
