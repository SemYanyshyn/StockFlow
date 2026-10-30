using System;
using System.Collections.Generic;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.BusinessLogic.Warehouses;
using StockFlow.DataAccess.Warehouses;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public sealed class WarehouseServiceTests
{
    private sealed class FakeWarehouseRepository : IWarehouseRepository
    {
        public List<Warehouse> Warehouses { get; } = new();
        public int AddCalls { get; private set; }
        public IReadOnlyList<Warehouse> GetAll() => Warehouses.ToArray();
        public int Add(Warehouse warehouse)
        {
            AddCalls++;
            Warehouses.Add(new Warehouse
            {
                Id = AddCalls,
                Name = warehouse.Name,
                Address = warehouse.Address
            });
            return AddCalls;
        }
    }

    private sealed class FakeUserContext : ICurrentUserContext
    {
        public bool IsAdmin { get; set; }
        public UserRole Role => IsAdmin ? UserRole.Admin : UserRole.Employee;
        public int UserId => IsAdmin ? 1 : 2;
        public User CurrentUser => new User
        {
            Id = UserId,
            Username = IsAdmin ? "admin" : "employee",
            PasswordHash = "hash",
            FullName = IsAdmin ? "Адміністратор" : "Працівник складу",
            Role = Role,
            IsActive = true
        };
    }


    [Fact]
    public void Add_WhenUserIsNotAdmin_ThrowsInvalidOperationException()
    {
        var repo = new FakeWarehouseRepository();
        var user = new FakeUserContext { IsAdmin = false };
        var service = new WarehouseService(repo, user);

        var ex = Assert.Throws<InvalidOperationException>(() => service.Add("Склад", "Адреса"));
        Assert.Equal("Додавати склади може лише Адміністратор.", ex.Message);
        Assert.Empty(repo.Warehouses);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_WhenNameIsEmptyOrWhitespace_ThrowsValidationException(string? name)
    {
        var repo = new FakeWarehouseRepository();
        var user = new FakeUserContext { IsAdmin = true };
        var service = new WarehouseService(repo, user);

        var ex = Assert.Throws<ValidationException>(() => service.Add(name, "Адреса"));
        Assert.Equal("Назва складу обов'язкова.", ex.Message);
        Assert.Empty(repo.Warehouses);
    }

    [Fact]
    public void Add_WhenValidData_TrimsStringsAndSavesToRepository()
    {
        var repo = new FakeWarehouseRepository();
        var user = new FakeUserContext { IsAdmin = true };
        var service = new WarehouseService(repo, user);

        var id = service.Add("  Склад 1  ", "   вул. Київська  ");
        Assert.Equal(1, id);
        Assert.Single(repo.Warehouses);
        Assert.Equal("Склад 1", repo.Warehouses[0].Name);
        Assert.Equal("вул. Київська", repo.Warehouses[0].Address);

        var id2 = service.Add("Склад 2", "   ");
        Assert.Equal(2, id2);
        Assert.Equal("Склад 2", repo.Warehouses[1].Name);
        Assert.Null(repo.Warehouses[1].Address);
    }

    [Fact]
    public void GetAll_ReturnsAllWarehousesFromRepository()
    {
        var repo = new FakeWarehouseRepository();
        repo.Warehouses.Add(new Warehouse { Id = 1, Name = "Склад 1", Address = "Адреса 1" });
        repo.Warehouses.Add(new Warehouse { Id = 2, Name = "Склад 2", Address = null });
        var user = new FakeUserContext { IsAdmin = false };
        var service = new WarehouseService(repo, user);

        var all = service.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal("Склад 1", all[0].Name);
        Assert.Equal("Склад 2", all[1].Name);
    }
}