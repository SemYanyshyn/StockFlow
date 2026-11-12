using StockFlow.BusinessLogic.Products;
using StockFlow.DataAccess.Users;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Users;

public sealed class AuthService
{
    public const string InvalidCredentialsMessage = "Невірний логін або пароль";
    public const string BlockedMessage = "Обліковий запис заблоковано. Зверніться до Адміністратора.";
    private readonly Func<IUserRepository> _repository;
    private readonly IPasswordHasher _hasher;
    private readonly AuthenticatedUserContext _context;

    public AuthService(Func<IUserRepository> repository, IPasswordHasher hasher, AuthenticatedUserContext context)
    {
        _repository = repository;
        _hasher = hasher;
        _context = context;
    }

    public void Login(string? username, string? password)
    {
        _context.Clear();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
            username.Trim().Length > User.MaxUsernameLength || password.Length > User.MaxPasswordLength)
            throw new ValidationException(InvalidCredentialsMessage);
        var user = _repository().GetByUsername(username.Trim());
        if (user is null || !_hasher.Verify(password, user.PasswordHash))
            throw new ValidationException(InvalidCredentialsMessage);
        if (!user.IsActive) throw new ValidationException(BlockedMessage);
        if (!Enum.IsDefined(user.Role)) throw new ValidationException(InvalidCredentialsMessage);
        _context.SignIn(user);
    }
}
