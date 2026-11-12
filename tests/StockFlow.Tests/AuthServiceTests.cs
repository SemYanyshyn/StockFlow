using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess.Users;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class AuthServiceTests
{
    private sealed class Repository : IUserRepository
    {
        public IReadOnlyList<User> GetAll() => User is null ? Array.Empty<User>() : new[] { User };
        public IUserManagementTransaction BeginManagement() => throw new NotSupportedException();
        public User? User { get; set; }
        public User? GetByUsername(string username) => User?.Username == username ? User : null;
    }

    [Fact]
    public void HashUsesRandomSaltAndVerifiesExactPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var first = hasher.Hash(" secret ");
        var second = hasher.Hash(" secret ");
        Assert.NotEqual(first, second);
        Assert.StartsWith("pbkdf2-sha256$v1$600000$", first);
        Assert.True(first.Length <= 255);
        Assert.True(hasher.Verify(" secret ", first));
        Assert.False(hasher.Verify("secret", first));
        Assert.False(hasher.Verify("wrong", first));
    }

    [Theory]
    [InlineData("")]
    [InlineData("placeholder-not-a-hash")]
    [InlineData("pbkdf2-sha256$v2$600000$a$b")]
    [InlineData("pbkdf2-sha256$v1$9999999999999$a$b")]
    [InlineData("pbkdf2-sha256$v1$2000001$a$b")]
    [InlineData("pbkdf2-sha256$v1$0$a$b")]
    [InlineData("pbkdf2-sha256$v1$600000$bad-base64$bad-base64")]
    [InlineData("pbkdf2-sha256$v1$600000$YQ==$Yg==")]
    public void MalformedHashesAreControlledFailures(string hash) =>
        Assert.False(new Pbkdf2PasswordHasher().Verify("password", hash));

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Employee)]
    public void LoginSetsMatchingContextWithoutExposingCredentials(UserRole role)
    {
        var hasher = new Pbkdf2PasswordHasher();
        var repository = new Repository { User = new User { Id = 42, Username = "demo", FullName = "Ім’я", Role = role, PasswordHash = hasher.Hash("Password!") } };
        var context = new AuthenticatedUserContext();
        var auth = new AuthService(() => repository, hasher, context);
        auth.Login(" demo ", "Password!");
        ICurrentUserContext current = context;
        Assert.Equal(42, current.UserId);
        Assert.Equal(role, current.Role);
        Assert.Equal(role == UserRole.Admin, current.IsAdmin);
        Assert.Empty(current.CurrentUser.PasswordHash);
        current.CurrentUser.Role = (UserRole)99;
        repository.User.Role = (UserRole)99;
        Assert.Equal(role, current.Role);
        Assert.Equal(AuthService.InvalidCredentialsMessage,
            Assert.Throws<ValidationException>(() => auth.Login("missing", "Password!")).Message);
        Assert.False(context.IsAuthenticated);
    }

    [Fact]
    public void UnknownWrongAndCorruptCredentialsHaveSameMessageAndBlockedStatusNeedsCorrectPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var user = new User { Username = "demo", PasswordHash = hasher.Hash("Password!"), IsActive = false };
        var repository = new Repository { User = user };
        var context = new AuthenticatedUserContext();
        var auth = new AuthService(() => repository, hasher, context);
        Assert.Equal(AuthService.InvalidCredentialsMessage, Assert.Throws<ValidationException>(() => auth.Login("missing", "Password!")).Message);
        Assert.Equal(AuthService.InvalidCredentialsMessage, Assert.Throws<ValidationException>(() => auth.Login("demo", "wrong")).Message);
        Assert.Equal(AuthService.BlockedMessage, Assert.Throws<ValidationException>(() => auth.Login("demo", "Password!")).Message);
        Assert.False(context.IsAuthenticated);
        user.IsActive = true;
        auth.Login("demo", "Password!");
        Assert.True(context.IsAuthenticated);
        user.PasswordHash = "corrupt";
        Assert.Equal(AuthService.InvalidCredentialsMessage, Assert.Throws<ValidationException>(() => auth.Login("demo", "Password!")).Message);
    }

    [Fact]
    public void RepositoryFailuresDoNotAuthenticate()
    {
        var context = new AuthenticatedUserContext();
        var auth = new AuthService(() => throw new InvalidOperationException("DB unavailable"), new Pbkdf2PasswordHasher(), context);
        Assert.Throws<InvalidOperationException>(() => auth.Login("demo", "Password!"));
        Assert.False(context.IsAuthenticated);
    }
}
