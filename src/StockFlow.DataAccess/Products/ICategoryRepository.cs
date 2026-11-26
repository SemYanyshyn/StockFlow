using StockFlow.Domain;

namespace StockFlow.DataAccess.Products;

public interface ICategoryRepository
{
    IReadOnlyList<Category> GetAll();
    int Add(Category category);
    bool Update(Category category);
    int CountProducts(int categoryId);
    bool Delete(int categoryId);
}
