using StockFlow.Domain;

namespace StockFlow.DataAccess.Suppliers;

public interface ISupplierRepository
{
    IReadOnlyList<Supplier> GetAll();

    int Add(Supplier supplier);
}
