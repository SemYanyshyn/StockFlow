using StockFlow.BusinessLogic.Products;
using StockFlow.DataAccess.Products;
using StockFlow.DataAccess.Users;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Users;

public sealed class UserService
{
    public const string AdminOnlyMessage = "Виконувати цю дію може лише Адміністратор.";
    public const string LastAdminMessage = "Не можна позбавити систему останнього активного Адміністратора.";
    public const string SelfBlockMessage = "Не можна заблокувати власний обліковий запис.";
    public const string SelfDeleteMessage = "Не можна видалити власний обліковий запис.";
    public const string InUseMessage = "Користувач має зареєстровані рухи товарів. Замість видалення заблокуйте обліковий запис.";
    public const string DuplicateMessage = "Користувач із таким логіном уже існує.";
    public const string NotFoundMessage = "Користувача вже видалено. Оновіть список.";
    private readonly IUserRepository _repository;
    private readonly ICurrentUserContext _current;
    private readonly IPasswordHasher _hasher;

    public UserService(IUserRepository repository, ICurrentUserContext current, IPasswordHasher hasher)
    {
        _repository = repository;
        _current = current;
        _hasher = hasher;
    }

    private void EnsureAdmin()
    {
        if (!_current.IsAdmin) throw new ValidationException(AdminOnlyMessage);
    }
    private IUserManagementTransaction Begin()
    {
        EnsureAdmin();
        var transaction = _repository.BeginManagement();
        // A second app may have blocked/demoted this actor since login.
        try
        {
            var actor = transaction.GetById(_current.UserId);
            if (actor is not { IsActive: true, Role: UserRole.Admin }) throw new ValidationException(AdminOnlyMessage);
            return transaction;
        }
        catch { transaction.Dispose(); throw; }
    }
    public IReadOnlyList<User> GetAll()
    {
        using var transaction = Begin();
        // The lock also keeps the actor check valid until this protected read completes.
        var users = transaction.GetAll();
        transaction.Commit();
        return users.Select(PublicUser).ToArray();
    }
    public IReadOnlyList<User> Filter(IEnumerable<User> users, string? query, UserRole? role)
    {
        EnsureAdmin();
        if (role.HasValue && !Enum.IsDefined(role.Value)) throw new ValidationException("Некоректна роль користувача.");
        return users.Where(u => u.MatchesSearch(query) && (!role.HasValue || u.Role == role)).ToArray();
    }
    public User Create(string? username, string? fullName, string? password, UserRole role)
    {
        EnsureAdmin();
        var user = new User { Username = username?.Trim() ?? "", FullName = fullName?.Trim() ?? "", Role = role };
        ValidateIdentity(user);
        if (string.IsNullOrWhiteSpace(password)) throw new ValidationException("Пароль обов’язковий.");
        if (password.Length > User.MaxPasswordLength) throw new ValidationException($"Пароль не може перевищувати {User.MaxPasswordLength} символів.");
        user.PasswordHash = _hasher.Hash(password);
        using var transaction = Begin();
        try { user.Id = transaction.Add(user); }
        catch (DuplicateUsernameException) { throw new ValidationException(DuplicateMessage); }
        transaction.Commit();
        return PublicUser(user);
    }
    public User Update(int id, string? fullName, UserRole role)
    {
        using var transaction = Begin();
        var user = Find(transaction, id);
        var oldRole = user.Role;
        user.FullName = fullName?.Trim() ?? "";
        user.Role = role;
        ValidateIdentity(user);
        if (oldRole == UserRole.Admin && user.IsActive && role != UserRole.Admin) ProtectLastAdmin(transaction);
        if (!transaction.Update(user)) throw new ValidationException(NotFoundMessage);
        transaction.Commit();
        if (id == _current.UserId && _current is AuthenticatedUserContext context) context.SignIn(user);
        return PublicUser(user);
    }
    public void SetActive(int id, bool active)
    {
        using var transaction = Begin();
        var user = Find(transaction, id);
        if (!active && id == _current.UserId) throw new ValidationException(SelfBlockMessage);
        if (!active && user.IsActive && user.Role == UserRole.Admin) ProtectLastAdmin(transaction);
        if (!transaction.SetActive(id, active)) throw new ValidationException(NotFoundMessage);
        transaction.Commit();
    }
    public void Delete(int id)
    {
        using var transaction = Begin();
        var user = Find(transaction, id);
        if (id == _current.UserId) throw new ValidationException(SelfDeleteMessage);
        if (user.IsActive && user.Role == UserRole.Admin) ProtectLastAdmin(transaction);
        if (transaction.HasMovements(id)) throw new ValidationException(InUseMessage);
        try
        {
            if (!transaction.Delete(id)) throw new ValidationException(NotFoundMessage);
        }
        catch (RecordInUseException) { throw new ValidationException(InUseMessage); }
        transaction.Commit();
    }
    private static User Find(IUserManagementTransaction transaction, int id) =>
        transaction.GetById(id) ?? throw new ValidationException(NotFoundMessage);
    private static void ProtectLastAdmin(IUserManagementTransaction transaction)
    {
        if (transaction.CountActiveAdmins() <= 1) throw new ValidationException(LastAdminMessage);
    }
    private static void ValidateIdentity(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Username)) throw new ValidationException("Логін обов’язковий.");
        if (user.Username.Length > User.MaxUsernameLength) throw new ValidationException($"Логін не може перевищувати {User.MaxUsernameLength} символів.");
        if (string.IsNullOrWhiteSpace(user.FullName)) throw new ValidationException("ПІБ обов’язкове.");
        if (user.FullName.Length > User.MaxFullNameLength) throw new ValidationException($"ПІБ не може перевищувати {User.MaxFullNameLength} символів.");
        if (!Enum.IsDefined(user.Role)) throw new ValidationException("Некоректна роль користувача.");
    }
    private static User PublicUser(User user) => new()
    {
        Id = user.Id, Username = user.Username, FullName = user.FullName, Role = user.Role, IsActive = user.IsActive
    };
}
