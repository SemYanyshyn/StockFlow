using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess.Suppliers;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Suppliers;

public sealed class SupplierService
{
    public const string AdminOnlyMessage = "Додавати постачальників може лише Адміністратор.";
    public const string NameRequiredMessage = "Назва постачальника обов'язкова.";
    public static readonly string NameTooLongMessage =
        $"Назва постачальника не може бути довшою за {Supplier.MaxNameLength} символів.";
    public static readonly string PhoneTooLongMessage =
        $"Телефон не може бути довшим за {Supplier.MaxPhoneLength} символів.";
    public const string InvalidEmailMessage = "Некоректна електронна пошта постачальника.";

    private readonly ISupplierRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public SupplierService(ISupplierRepository repository, ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(currentUser);
        _repository = repository;
        _currentUser = currentUser;
    }

    public IReadOnlyList<Supplier> GetAll() => _repository.GetAll();

    public Supplier Add(string? name, string? phone = null, string? email = null)
    {
        if (!_currentUser.IsAdmin)
        {
            throw new ValidationException(AdminOnlyMessage);
        }

        var supplier = new Supplier
        {
            Name = name?.Trim() ?? string.Empty,
            Phone = NormalizeOptional(phone),
            Email = NormalizeOptional(email)
        };

        if (!supplier.Validate())
        {
            if (supplier.Name.Length == 0)
            {
                throw new ValidationException(NameRequiredMessage);
            }
            if (supplier.Name.Length > Supplier.MaxNameLength)
            {
                throw new ValidationException(NameTooLongMessage);
            }
            if (supplier.Phone?.Length > Supplier.MaxPhoneLength)
            {
                throw new ValidationException(PhoneTooLongMessage);
            }
            throw new ValidationException(InvalidEmailMessage);
        }

        supplier.Id = _repository.Add(supplier);
        return supplier;
    }

    public static IReadOnlyList<Supplier> Filter(IEnumerable<Supplier> suppliers, string? query)
    {
        ArgumentNullException.ThrowIfNull(suppliers);
        return suppliers.Where(supplier => supplier.MatchesSearch(query)).ToList();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
