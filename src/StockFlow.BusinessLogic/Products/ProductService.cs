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

        var product = new Product { Name = trimmedName, CategoryId = categoryId.Value, Unit = unit.Value };
        if (!product.Validate())
        {
            throw new ValidationException(InvalidProductMessage);
        }

        product.Id = _repository.Add(product);
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
