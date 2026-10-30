using StockFlow.Domain;

namespace StockFlow.DataAccess.Products;

public interface IProductRepository
{
    IReadOnlyList<Product> GetAll();
    int Add(Product product);
    Product? GetById(int productId);
    bool Update(Product product);
    bool HasRelatedRecords(int productId);
    bool Delete(int productId);
}
