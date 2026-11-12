using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess.Products;
using StockFlow.DataAccess.Users;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class UserServiceTests
{
    private sealed class Hasher : IPasswordHasher
    {
        public int Calls { get; private set; }
        public string Hash(string password) { Calls++; return "encoded-password"; }
        public bool Verify(string password, string encodedHash) => false;
    }
    private sealed class Repository : IUserRepository
    {
        public List<User> Users { get; } = new()
        {
            new User { Id = 1, Username = "admin", FullName = "Адміністратор", PasswordHash = "hash", Role = UserRole.Admin },
            new User { Id = 2, Username = "employee", FullName = "Іван Петренко", PasswordHash = "existing-hash", Role = UserRole.Employee }
        };
        public HashSet<int> Movements { get; } = new();
        public bool ForeignKeyFailure { get; set; }
        public int Commits { get; private set; }
        public int Begins { get; private set; }
        public int Disposals { get; private set; }
        public User? GetByUsername(string username) => Users.FirstOrDefault(u => u.Username == username);
        public IReadOnlyList<User> GetAll() => Users.Select(Copy).ToArray();
        public IUserManagementTransaction BeginManagement() { Begins++; return new Transaction(this); }
        private static User Copy(User user) => new()
        {
            Id = user.Id, Username = user.Username, FullName = user.FullName, PasswordHash = user.PasswordHash,
            Role = user.Role, IsActive = user.IsActive
        };
        private sealed class Transaction : IUserManagementTransaction
        {
            private readonly Repository _repository;
            private readonly List<User> _snapshot;
            public Transaction(Repository repository) { _repository = repository; _snapshot = repository.Users.Select(Copy).ToList(); }
            public User? GetById(int id) => _snapshot.Where(u => u.Id == id).Select(Copy).FirstOrDefault();
            public IReadOnlyList<User> GetAll() => _snapshot.Select(Copy).ToArray();
            public int CountActiveAdmins() => _snapshot.Count(u => u.IsActive && u.Role == UserRole.Admin);
            public bool HasMovements(int id) => _repository.Movements.Contains(id);
            public int Add(User user)
            {
                if (_snapshot.Any(u => u.Username == user.Username)) throw new DuplicateUsernameException();
                var saved = Copy(user); saved.Id = _snapshot.Max(u => u.Id) + 1; _snapshot.Add(saved); return saved.Id;
            }
            public bool Update(User user)
            {
                var saved = _snapshot.Single(u => u.Id == user.Id);
                saved.FullName = user.FullName; saved.Role = user.Role; return true;
            }
            public bool SetActive(int id, bool active) { _snapshot.Single(u => u.Id == id).IsActive = active; return true; }
            public bool Delete(int id)
            {
                if (_repository.ForeignKeyFailure) throw new RecordInUseException();
                return _snapshot.RemoveAll(u => u.Id == id) == 1;
            }
            public void Commit() { _repository.Users.Clear(); _repository.Users.AddRange(_snapshot); _repository.Commits++; }
            public void Dispose() => _repository.Disposals++;
        }
    }
    private static (UserService Service, Repository Repository, Hasher Hasher) Create(UserRole role = UserRole.Admin)
    {
        var repository = new Repository(); var hasher = new Hasher();
        return (new UserService(repository, new TemporaryUserContext(role), hasher), repository, hasher);
    }

    [Fact]
    public void CreateTrimsIdentityHashesPasswordAndPreservesCaseSensitiveUsernameRule()
    {
        var (service, repository, hasher) = Create();
        var user = service.Create(" NewUser ", " Новий Користувач ", "secret", UserRole.Employee);
        Assert.Equal("NewUser", user.Username); Assert.Equal("Новий Користувач", user.FullName);
        Assert.Empty(user.PasswordHash);
        Assert.Equal("encoded-password", repository.Users.Single(u => u.Id == user.Id).PasswordHash);
        Assert.Equal(1, hasher.Calls);
        Assert.Equal(UserService.DuplicateMessage, Assert.Throws<ValidationException>(() => service.Create("NewUser", "Name", "secret", UserRole.Employee)).Message);
        service.Create("newuser", "Name", "secret", UserRole.Employee);
        Assert.Equal(4, repository.Users.Count);
    }

    [Theory]
    [InlineData("", "Name", "secret", UserRole.Employee)]
    [InlineData("name", " ", "secret", UserRole.Employee)]
    [InlineData("name", "Name", " ", UserRole.Employee)]
    [InlineData("name", "Name", "secret", (UserRole)99)]
    public void CreateRejectsInvalidFieldsWithoutWriting(string username, string name, string password, UserRole role)
    {
        var (service, repository, _) = Create();
        Assert.Throws<ValidationException>(() => service.Create(username, name, password, role));
        Assert.Equal(0, repository.Commits);
        Assert.Equal(2, repository.Users.Count);
    }

    [Fact]
    public void LengthLimitsAreCheckedBeforeHashingAndPersistence()
    {
        var (service, repository, hasher) = Create();
        Assert.Throws<ValidationException>(() => service.Create(new string('u', 51), "Name", "secret", UserRole.Employee));
        Assert.Throws<ValidationException>(() => service.Create("name", new string('n', 101), "secret", UserRole.Employee));
        Assert.Throws<ValidationException>(() => service.Create("name", "Name", new string('p', 1025), UserRole.Employee));
        Assert.Throws<ValidationException>(() => service.Update(2, new string('n', 101), UserRole.Employee));
        Assert.Throws<ValidationException>(() => service.Update(2, "Name", (UserRole)99));
        Assert.Equal(0, repository.Commits); Assert.Equal(0, hasher.Calls);
    }

    [Fact]
    public void EditChangesOnlyNameAndRoleAndCancellationByCallerLeavesOriginalUntouched()
    {
        var (service, repository, _) = Create();
        var original = repository.Users.Single(u => u.Id == 2);
        var updated = service.Update(2, " Оновлене Ім’я ", UserRole.Admin);
        var stored = repository.Users.Single(u => u.Id == 2);
        Assert.Equal("employee", stored.Username); Assert.Equal("existing-hash", stored.PasswordHash);
        Assert.Equal("Оновлене Ім’я", updated.FullName); Assert.Equal(UserRole.Admin, stored.Role);
        Assert.Empty(updated.PasswordHash); Assert.Equal("Іван Петренко", original.FullName);
        Assert.Throws<ValidationException>(() => service.Update(2, " ", UserRole.Employee));
        Assert.Equal("Оновлене Ім’я", repository.Users.Single(u => u.Id == 2).FullName);
    }

    [Theory]
    [InlineData("ІВАН", null, 1)]
    [InlineData("EMPLOYEE", UserRole.Employee, 1)]
    [InlineData(" employee ", UserRole.Admin, 0)]
    [InlineData(" ", UserRole.Admin, 1)]
    [InlineData(null, null, 2)]
    public void SearchAndRoleFiltersCombine(string? query, UserRole? role, int count)
    {
        var (service, _, _) = Create();
        Assert.Equal(count, service.Filter(service.GetAll(), query, role).Count);
        Assert.All(service.GetAll(), u => Assert.Empty(u.PasswordHash));
    }

    [Fact]
    public void EmployeeCannotCallAnyManagementOperation()
    {
        var (service, repository, hasher) = Create(UserRole.Employee);
        Assert.Throws<ValidationException>(() => service.GetAll());
        Assert.Throws<ValidationException>(() => service.Filter(Array.Empty<User>(), null, null));
        Assert.Throws<ValidationException>(() => service.Create("name", "Name", "secret", UserRole.Admin));
        Assert.Throws<ValidationException>(() => service.Update(1, "Name", UserRole.Employee));
        Assert.Throws<ValidationException>(() => service.SetActive(1, false));
        Assert.Throws<ValidationException>(() => service.Delete(1));
        Assert.Equal(0, repository.Begins); Assert.Equal(0, hasher.Calls);
    }

    [Fact]
    public void StaleAdminSessionCannotReadOrWriteWhenBlockedOrDemotedInDatabase()
    {
        var (service, repository, _) = Create();
        repository.Users[0].IsActive = false;
        Assert.Equal(UserService.AdminOnlyMessage, Assert.Throws<ValidationException>(() => service.GetAll()).Message);
        Assert.Throws<ValidationException>(() => service.Update(2, "Name", UserRole.Admin));
        repository.Users[0].IsActive = true; repository.Users[0].Role = UserRole.Employee;
        Assert.Throws<ValidationException>(() => service.SetActive(2, false));
        Assert.Equal(0, repository.Commits); Assert.Equal(repository.Begins, repository.Disposals);
    }

    [Fact]
    public void LastActiveAdminCannotBeDemotedButCanChangeName()
    {
        var (service, repository, _) = Create();
        Assert.Equal(UserService.LastAdminMessage, Assert.Throws<ValidationException>(() => service.Update(1, "Name", UserRole.Employee)).Message);
        Assert.Equal(UserRole.Admin, repository.Users[0].Role);
        service.Update(1, "Name", UserRole.Admin);
        service.SetActive(2, false);
        Assert.Equal(UserService.LastAdminMessage, Assert.Throws<ValidationException>(() => service.Update(1, "Name", UserRole.Employee)).Message);
        service.SetActive(2, true);
        service.Update(2, "Second admin", UserRole.Admin);
        service.Update(1, "Name", UserRole.Employee);
        Assert.Single(repository.Users, u => u.IsActive && u.Role == UserRole.Admin);
    }

    [Fact]
    public void CannotBlockOrDeleteSelfAndCanBlockUnblockOtherUsers()
    {
        var (service, repository, _) = Create();
        Assert.Equal(UserService.SelfBlockMessage, Assert.Throws<ValidationException>(() => service.SetActive(1, false)).Message);
        Assert.Equal(UserService.SelfDeleteMessage, Assert.Throws<ValidationException>(() => service.Delete(1)).Message);
        service.SetActive(2, false); Assert.False(repository.Users[1].IsActive);
        service.SetActive(2, true); Assert.True(repository.Users[1].IsActive);
    }

    [Fact]
    public void DeleteChecksMovementsAndTranslatesForeignKeyRaceWithoutCommit()
    {
        var (service, repository, _) = Create();
        repository.Movements.Add(2);
        Assert.Equal(UserService.InUseMessage, Assert.Throws<ValidationException>(() => service.Delete(2)).Message);
        repository.Movements.Clear(); repository.ForeignKeyFailure = true;
        Assert.Equal(UserService.InUseMessage, Assert.Throws<ValidationException>(() => service.Delete(2)).Message);
        Assert.Equal(0, repository.Commits);
        repository.ForeignKeyFailure = false; service.Delete(2);
        Assert.Single(repository.Users);
        Assert.Equal(UserService.NotFoundMessage, Assert.Throws<ValidationException>(() => service.Update(2, "Name", UserRole.Employee)).Message);
        Assert.Equal(UserService.NotFoundMessage, Assert.Throws<ValidationException>(() => service.Delete(2)).Message);
    }
}
