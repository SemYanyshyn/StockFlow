using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess.Suppliers;
using StockFlow.DataAccess.Products;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Suppliers;

public sealed class SupplierService
{
    public const string AdminOnlyMessage = "Виконувати цю дію може лише Адміністратор.";
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

        var supplier = Build(name, phone, email);
        supplier.Id = _repository.Add(supplier);
        return supplier;
    }

    private static Supplier Build(string? name, string? phone, string? email)
    {
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

        return supplier;
    }

    public Supplier Update(int id, string? name, string? phone = null, string? email = null)
    {
        EnsureAdmin();
        var supplier = Build(name, phone, email);
        supplier.Id = id;
        if (!_repository.Update(supplier)) throw new ValidationException("Постачальника вже видалено. Оновіть список.");
        return supplier;
    }

    public void Delete(int id)
    {
        EnsureAdmin();
        try
        {
            if (!_repository.Delete(id)) throw new ValidationException("Постачальника вже видалено. Оновіть список.");
        }
        catch (RecordInUseException)
        { throw new ValidationException("Неможливо видалити постачальника з рухами товарів."); }
    }

    private void EnsureAdmin()
    {
        if (!_currentUser.IsAdmin) throw new ValidationException(AdminOnlyMessage);
    }

    public static IReadOnlyList<Supplier> Filter(IEnumerable<Supplier> suppliers, string? query)
    {
        ArgumentNullException.ThrowIfNull(suppliers);
        return suppliers.Where(supplier => supplier.MatchesSearch(query)).ToList();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
