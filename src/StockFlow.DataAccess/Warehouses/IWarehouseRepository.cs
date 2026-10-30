using System.Collections.Generic;
using StockFlow.Domain;

namespace StockFlow.DataAccess.Warehouses;

public interface IWarehouseRepository
{
    IReadOnlyList<Warehouse> GetAll();
    int Add(Warehouse warehouse);
}