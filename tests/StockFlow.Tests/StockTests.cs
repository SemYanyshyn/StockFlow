using System.Data;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess;
using StockFlow.DataAccess.Products;
using StockFlow.DataAccess.Warehouses;
using StockFlow.Domain;
using Xunit;

namespace StockFlow.Tests;

public class StockTests
{
    [Theory]
    [InlineData(4, 10, true)]
    [InlineData(10, 10, false)]
    [InlineData(15, 10, false)]
    [InlineData(0, 0, false)]
    public void IsBelowThreshold_EvaluatesCorrectly(int quantity, int threshold, bool expected)
    {
        var stock = new Stock { Quantity = quantity, MinThreshold = threshold };
        Assert.Equal(expected, stock.IsLowStock());
    }

    [Fact]
    public void Validate_ValidStock_ReturnsTrue()
    {
        var stock = new Stock { ProductId = 1, WarehouseId = 1, Quantity = 5, MinThreshold = 2 };
        Assert.True(stock.Validate());
    }
}

public sealed class StockServiceTests
{
    private sealed class FakeStockRepository : IStockRepository
    {
        public List<Stock> Items { get; } = new();
        public int GetAllCalls { get; private set; }
        public int SetMinThresholdCalls { get; private set; }
        public int Calls { get; private set; }

        public Stock GetForUpdate(int productId, int warehouseId, IDbTransaction transaction)
        {
            Calls++;
            throw new NotSupportedException();
        }

        public void Save(Stock stock, IDbTransaction transaction)
        {
            Calls++;
            throw new NotSupportedException();
        }

        public void SetMinThreshold(int productId, int warehouseId, int minThreshold)
        {
            Calls++;
            SetMinThresholdCalls++;
            var stock = Items.SingleOrDefault(item =>
                item.ProductId == productId && item.WarehouseId == warehouseId);
            if (stock is null)
            {
                Items.Add(new Stock
                {
                    ProductId = productId,
                    WarehouseId = warehouseId,
                    Quantity = 0,
                    MinThreshold = minThreshold
                });
            }
            else
            {
                stock.MinThreshold = minThreshold;
            }
        }

        public IReadOnlyList<Stock> GetAll(int? warehouseId = null, IDbTransaction? transaction = null)
        {
            Calls++;
            GetAllCalls++;
            return warehouseId.HasValue
                ? Items.Where(s => s.WarehouseId == warehouseId.Value).ToList()
                : Items.ToList();
        }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Items { get; } = new();
        public int GetAllCalls { get; private set; }

        public IReadOnlyList<Product> GetAll()
        {
            GetAllCalls++;
            return Items;
        }

        public Product? GetById(int id) => Items.FirstOrDefault(p => p.Id == id);
        public int Add(Product product) => throw new NotSupportedException();
        public bool Update(Product product) => throw new NotSupportedException();
        public bool Delete(int id) => throw new NotSupportedException();
        public bool HasRelatedRecords(int id) => false;
    }

    private sealed class FakeWarehouseRepository : IWarehouseRepository
    {
        public List<Warehouse> Items { get; } = new();
        public int GetAllCalls { get; private set; }

        public IReadOnlyList<Warehouse> GetAll()
        {
            GetAllCalls++;
            return Items;
        }

        public int Add(Warehouse warehouse) => throw new NotSupportedException();
        public bool Update(Warehouse warehouse) => throw new NotSupportedException();
        public bool Delete(int id) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUserContext(UserRole role) : ICurrentUserContext
    {
        public User CurrentUser { get; } = new() { Id = 1, Role = role };
    }

    [Fact]
    public void IsBelowThreshold_QuantityBelowThreshold_ReturnsTrue()
    {
        _ = CreateService(out _, out _, out _);
        Assert.True(StockService.IsBelowThreshold(quantity: 4, minThreshold: 10));
    }

    [Fact]
    public void IsBelowThreshold_QuantityEqualsThreshold_ReturnsFalse()
    {
        _ = CreateService(out _, out _, out _);
        Assert.False(StockService.IsBelowThreshold(quantity: 10, minThreshold: 10));
    }

    [Fact]
    public void IsBelowThreshold_QuantityAboveThreshold_ReturnsFalse()
    {
        _ = CreateService(out _, out _, out _);
        Assert.False(StockService.IsBelowThreshold(quantity: 15, minThreshold: 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    public void IsBelowThreshold_ThresholdIsZero_ReturnsFalse(int quantity)
    {
        _ = CreateService(out _, out _, out _);
        Assert.False(StockService.IsBelowThreshold(quantity: quantity, minThreshold: 0));
    }

    [Fact]
    public void SetMinThreshold_EmployeeIsDeniedBeforeRepositoryCall()
    {
        var service = CreateService(out var stockRepo, out _, out _, UserRole.Employee);

        var error = Assert.Throws<InvalidOperationException>(() => service.SetMinThreshold(1, 1, 5));

        Assert.Equal(StockService.MinThresholdAdminOnlyMessage, error.Message);
        Assert.Equal(0, stockRepo.Calls);
    }

    [Fact]
    public void SetMinThreshold_NegativeValueIsRejectedBeforeRepositoryCall()
    {
        var service = CreateService(out var stockRepo, out _, out _);

        var error = Assert.Throws<ValidationException>(() => service.SetMinThreshold(1, 1, -1));

        Assert.Equal(StockService.NegativeMinThresholdMessage, error.Message);
        Assert.Equal(0, stockRepo.Calls);
    }

    [Fact]
    public void SetMinThreshold_ZeroDisablesCriticalStatusForEmptyStock()
    {
        var service = CreateService(out var stockRepo, out _, out _);
        var stock = new Stock { ProductId = 1, WarehouseId = 1, Quantity = 0, MinThreshold = 5 };
        stockRepo.Items.Add(stock);

        service.SetMinThreshold(1, 1, 0);

        Assert.Equal(0, stock.MinThreshold);
        Assert.False(StockService.IsBelowThreshold(stock.Quantity, stock.MinThreshold));
        Assert.Equal(1, stockRepo.SetMinThresholdCalls);
    }

    [Fact]
    public void SetMinThreshold_AdminUpdatePreservesQuantityAndRecalculatesCriticalStatus()
    {
        var service = CreateService(out var stockRepo, out var productRepo, out var warehouseRepo);
        var stock = new Stock { ProductId = 1, WarehouseId = 1, Quantity = 4, MinThreshold = 5 };
        stockRepo.Items.Add(stock);
        productRepo.Items.Add(new Product { Id = 1, Name = "Товар", Unit = UnitOfMeasure.Piece, CategoryId = 1 });
        warehouseRepo.Items.Add(new Warehouse { Id = 1, Name = "Склад" });

        Assert.True(service.GetRows().Single().IsLow);
        service.SetMinThreshold(1, 1, 3);
        var updatedRow = Assert.Single(service.GetRows());

        Assert.Equal(4, updatedRow.Quantity);
        Assert.Equal(3, updatedRow.MinThreshold);
        Assert.False(updatedRow.IsLow);
        Assert.Equal(1, stockRepo.SetMinThresholdCalls);
    }

    [Fact]
    public void GetRows_WithoutFilter_ReturnsAllWarehousesInStableOrder()
    {
        var service = CreateService(out var stockRepo, out var productRepo, out var warehouseRepo);
        productRepo.Items.AddRange(new[]
        {
            new Product { Id = 1, Name = "Яблука", Unit = UnitOfMeasure.Kilogram, CategoryId = 1 },
            new Product { Id = 2, Name = "Гречка", Unit = UnitOfMeasure.Kilogram, CategoryId = 1 }
        });
        warehouseRepo.Items.AddRange(new[]
        {
            new Warehouse { Id = 10, Name = "Склад №2" },
            new Warehouse { Id = 20, Name = "Основний склад" }
        });
        stockRepo.Items.AddRange(new[]
        {
            new Stock { ProductId = 1, WarehouseId = 10, Quantity = 15, MinThreshold = 5 },
            new Stock { ProductId = 2, WarehouseId = 20, Quantity = 120, MinThreshold = 20 },
            new Stock { ProductId = 2, WarehouseId = 10, Quantity = 30, MinThreshold = 20 }
        });

        var rows = service.GetRows();

        Assert.Equal(3, rows.Count);
        Assert.Equal("Гречка", rows[0].ProductName);
        Assert.Equal("Основний склад", rows[0].WarehouseName);
        Assert.Equal("Гречка", rows[1].ProductName);
        Assert.Equal("Склад №2", rows[1].WarehouseName);
        Assert.Equal("Яблука", rows[2].ProductName);
    }

    [Fact]
    public void GetRows_SingleWarehouse_ReturnsOnlyItsRows()
    {
        var service = CreateService(out var stockRepo, out var productRepo, out var warehouseRepo);
        productRepo.Items.Add(new Product { Id = 1, Name = "Кефір", Unit = UnitOfMeasure.Liter, CategoryId = 1 });
        warehouseRepo.Items.AddRange(new[]
        {
            new Warehouse { Id = 1, Name = "Основний склад" },
            new Warehouse { Id = 2, Name = "Склад №2" }
        });
        stockRepo.Items.AddRange(new[]
        {
            new Stock { ProductId = 1, WarehouseId = 1, Quantity = 4, MinThreshold = 10 },
            new Stock { ProductId = 1, WarehouseId = 2, Quantity = 25, MinThreshold = 10 }
        });

        var rows = service.GetRows(warehouseId: 2);

        var single = Assert.Single(rows);
        Assert.Equal("Склад №2", single.WarehouseName);
        Assert.Equal(25, single.Quantity);
    }

    [Fact]
    public void GetRows_WarehouseWithoutRecords_ReturnsEmptyList()
    {
        var service = CreateService(out var stockRepo, out var productRepo, out var warehouseRepo);
        productRepo.Items.Add(new Product { Id = 1, Name = "Молоко", Unit = UnitOfMeasure.Liter, CategoryId = 1 });
        warehouseRepo.Items.Add(new Warehouse { Id = 99, Name = "Порожній склад" });
        stockRepo.Items.Add(new Stock { ProductId = 1, WarehouseId = 1, Quantity = 10, MinThreshold = 5 });

        var rows = service.GetRows(warehouseId: 99);

        Assert.Empty(rows);
    }

    [Fact]
    public void GetRows_IsLow_MatchesIsBelowThreshold()
    {
        var service = CreateService(out var stockRepo, out var productRepo, out var warehouseRepo);
        productRepo.Items.AddRange(new[]
        {
            new Product { Id = 1, Name = "Нижче норми", Unit = UnitOfMeasure.Piece, CategoryId = 1 },
            new Product { Id = 2, Name = "Рівно норма", Unit = UnitOfMeasure.Piece, CategoryId = 1 },
            new Product { Id = 3, Name = "Поріг нуль", Unit = UnitOfMeasure.Piece, CategoryId = 1 }
        });
        warehouseRepo.Items.Add(new Warehouse { Id = 1, Name = "Основний склад" });
        stockRepo.Items.AddRange(new[]
        {
            new Stock { ProductId = 1, WarehouseId = 1, Quantity = 3, MinThreshold = 5 },
            new Stock { ProductId = 2, WarehouseId = 1, Quantity = 5, MinThreshold = 5 },
            new Stock { ProductId = 3, WarehouseId = 1, Quantity = 0, MinThreshold = 0 }
        });

        var rows = service.GetRows();

        Assert.True(rows.Single(r => r.ProductName == "Нижче норми").IsLow);
        Assert.False(rows.Single(r => r.ProductName == "Рівно норма").IsLow);
        Assert.False(rows.Single(r => r.ProductName == "Поріг нуль").IsLow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void GetRows_InvalidWarehouseId_ThrowsValidationExceptionAndDoesNotCallRepositories(int invalidWarehouseId)
    {
        var service = CreateService(out var stockRepo, out var productRepo, out var warehouseRepo);

        var ex = Assert.Throws<ValidationException>(() => service.GetRows(invalidWarehouseId));

        Assert.Equal("Некоректний ідентифікатор складу.", ex.Message);
        Assert.Equal(0, stockRepo.GetAllCalls);
        Assert.Equal(0, productRepo.GetAllCalls);
        Assert.Equal(0, warehouseRepo.GetAllCalls);
    }

    private static StockService CreateService(
        out FakeStockRepository stockRepo,
        out FakeProductRepository productRepo,
        out FakeWarehouseRepository warehouseRepo,
        UserRole role = UserRole.Admin)
    {
        stockRepo = new FakeStockRepository();
        productRepo = new FakeProductRepository();
        warehouseRepo = new FakeWarehouseRepository();
        return new StockService(stockRepo, productRepo, warehouseRepo, new FakeCurrentUserContext(role));
    }
}
