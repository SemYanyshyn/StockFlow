using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Suppliers;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess.Suppliers;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class SupplierServiceTests
{
    private sealed class FakeSupplierRepository : ISupplierRepository
    {
        public List<Supplier> Suppliers { get; } = new();
        public int AddCalls { get; private set; }

        public IReadOnlyList<Supplier> GetAll() => Suppliers.ToList();

        public int Add(Supplier supplier)
        {
            AddCalls++;
            var id = Suppliers.Count + 1;
            Suppliers.Add(new Supplier
            {
                Id = id,
                Name = supplier.Name,
                Phone = supplier.Phone,
                Email = supplier.Email
            });
            return id;
        }
    }

    private static (SupplierService Service, FakeSupplierRepository Repository) Create(UserRole role = UserRole.Admin)
    {
        var repository = new FakeSupplierRepository();
        return (new SupplierService(repository, new TemporaryUserContext(role)), repository);
    }

    [Fact]
    public void MissingRepositoryIsRejected() =>
        Assert.Throws<ArgumentNullException>(() =>
            new SupplierService(null!, new TemporaryUserContext(UserRole.Admin)));

    [Fact]
    public void MissingUserContextIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new SupplierService(new FakeSupplierRepository(), null!));

    [Fact]
    public void EmployeeCanReadSuppliers()
    {
        var (service, repository) = Create(UserRole.Employee);
        repository.Suppliers.Add(new Supplier { Id = 1, Name = "ТОВ Молоко" });
        Assert.Equal("ТОВ Молоко", Assert.Single(service.GetAll()).Name);
    }

    [Fact]
    public void EmployeeCannotAddSupplier()
    {
        var (service, repository) = Create(UserRole.Employee);
        var error = Assert.Throws<ValidationException>(() => service.Add("ТОВ Молоко"));
        Assert.Equal(SupplierService.AdminOnlyMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Fact]
    public void AdminCanAddSupplierWithTrimmedContactData()
    {
        var (service, repository) = Create();
        var added = service.Add("  ТОВ Молоко  ", "  +380 50 123 45 67  ", "  Sales@Milk.example  ");
        Assert.Equal(1, added.Id);
        Assert.Equal("ТОВ Молоко", added.Name);
        Assert.Equal("+380 50 123 45 67", added.Phone);
        Assert.Equal("Sales@Milk.example", added.Email);
        Assert.Equal(1, repository.AddCalls);
        Assert.Equal(added.Email, Assert.Single(repository.Suppliers).Email);
    }

    [Fact]
    public void BlankOptionalFieldsBecomeNull()
    {
        var (service, repository) = Create();
        var added = service.Add("ФОП Сад", "  ", "\t");
        Assert.Null(added.Phone);
        Assert.Null(added.Email);
        Assert.Null(Assert.Single(repository.Suppliers).Phone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingNameIsRejectedWithoutSaving(string? name)
    {
        var (service, repository) = Create();
        var error = Assert.Throws<ValidationException>(() => service.Add(name));
        Assert.Equal(SupplierService.NameRequiredMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Fact]
    public void TooLongNameIsRejectedWithoutSaving()
    {
        var (service, repository) = Create();
        var error = Assert.Throws<ValidationException>(() => service.Add(new string('а', Supplier.MaxNameLength + 1)));
        Assert.Equal(SupplierService.NameTooLongMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Fact]
    public void TooLongPhoneIsRejectedWithoutSaving()
    {
        var (service, repository) = Create();
        var error = Assert.Throws<ValidationException>(() => service.Add("ФОП Сад", new string('1', Supplier.MaxPhoneLength + 1)));
        Assert.Equal(SupplierService.PhoneTooLongMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Theory]
    [InlineData("bad-email")]
    [InlineData("@example.com")]
    [InlineData("name@")]
    [InlineData("a@b@c.com")]
    [InlineData("hello @example.com")]
    public void InvalidEmailIsRejectedWithoutSaving(string email)
    {
        var (service, repository) = Create();
        var error = Assert.Throws<ValidationException>(() => service.Add("ФОП Сад", email: email));
        Assert.Equal(SupplierService.InvalidEmailMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Fact]
    public void TooLongEmailIsRejectedWithoutSaving()
    {
        var (service, repository) = Create();
        var email = new string('x', Supplier.MaxEmailLength - 8) + "@mail.com";
        var error = Assert.Throws<ValidationException>(() => service.Add("ФОП Сад", email: email));
        Assert.Equal(SupplierService.InvalidEmailMessage, error.Message);
        Assert.Equal(0, repository.AddCalls);
    }

    [Theory]
    [InlineData("молок")]
    [InlineData("МОЛОКО")]
    [InlineData("38050")]
    [InlineData("DAIRY.EXAMPLE")]
    [InlineData("  milk  ")]
    public void FilterMatchesNamePhoneAndEmailIgnoringCase(string query)
    {
        var suppliers = new[]
        {
            new Supplier { Name = "ТОВ Молоко", Phone = "+380501234567", Email = "milk@dairy.example" },
            new Supplier { Name = "ФОП Яблуко", Phone = "+380671234567" }
        };
        var filtered = SupplierService.Filter(suppliers, query);
        Assert.Equal("ТОВ Молоко", Assert.Single(filtered).Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FilterWithBlankQueryReturnsAllSuppliers(string? query)
    {
        var suppliers = new[]
        {
            new Supplier { Name = "Перший" },
            new Supplier { Name = "Другий" }
        };
        Assert.Equal(2, SupplierService.Filter(suppliers, query).Count);
    }

    [Fact]
    public void FilterWithoutMatchesReturnsEmpty()
    {
        var suppliers = new[] { new Supplier { Name = "ФОП Яблуко" } };
        Assert.Empty(SupplierService.Filter(suppliers, "молоко"));
    }

    [Fact]
    public void FilterRejectsNullCollection() =>
        Assert.Throws<ArgumentNullException>(() => SupplierService.Filter(null!, "тест"));
}
