using System;
using System.Collections.Generic;
using StockFlow.DataAccess.Warehouses;
using StockFlow.Domain;
using StockFlow.BusinessLogic.Users;

namespace StockFlow.BusinessLogic.Warehouses;

public sealed class WarehouseService
{
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ICurrentUserContext _currentUserContext;

    public WarehouseService(IWarehouseRepository warehouseRepository, ICurrentUserContext currentUserContext)
    {
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
        _currentUserContext = currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
    }

    public IReadOnlyList<Warehouse> GetAll() => _warehouseRepository.GetAll();

    public int Add(string? name, string? address)
    {
        if (!_currentUserContext.IsAdmin)
            throw new InvalidOperationException("Додавати склади може лише Адміністратор.");

        name = name?.Trim();
        address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Назва складу обов'язкова.");

        var warehouse = new Warehouse
        {
            Name = name,
            Address = address
        };

        return _warehouseRepository.Add(warehouse);
    }
}