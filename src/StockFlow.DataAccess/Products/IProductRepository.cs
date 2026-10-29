using StockFlow.Domain;

namespace StockFlow.DataAccess.Products;

public interface IProductRepository
{
    IReadOnlyList<Product> GetAll();
}
