using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Suppliers;
using StockFlow.BusinessLogic.Users;
using StockFlow.BusinessLogic.Warehouses;
using StockFlow.DataAccess;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic;

public sealed record StockMovementEditorData(
    string Number, string ResponsibleName, IReadOnlyList<Product> Products,
    IReadOnlyList<Warehouse> Warehouses, IReadOnlyList<Supplier> Suppliers);

public interface IStockMovementEditorService
{
    StockMovementEditorData Load();
    StockMovementRegistrationResult Save(StockMovement request);
}

public sealed class StockMovementEditorService(
    ProductService products,
    WarehouseService warehouses,
    SupplierService suppliers,
    StockMovementNumberRepository numbers,
    ICurrentUserContext user,
    StockMovementService registration) : IStockMovementEditorService
{
    public StockMovementEditorData Load()
    {
        var responsible = user.CurrentUser;
        if (responsible.Role != UserRole.Employee)
            throw new ValidationException("Створювати рух товару може лише Працівник");

        var productOptions = products.GetAll();
        var warehouseOptions = warehouses.GetAll();
        if (productOptions.Count == 0 || warehouseOptions.Count == 0)
            throw new ValidationException("Для створення накладної додайте товар і склад у довідниках.");

        return new(numbers.Reserve(), responsible.FullName, productOptions, warehouseOptions, suppliers.GetAll());
    }

    public StockMovementRegistrationResult Save(StockMovement request) => registration.Register(request);
}
