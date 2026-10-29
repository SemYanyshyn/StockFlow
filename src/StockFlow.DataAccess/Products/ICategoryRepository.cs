using StockFlow.Domain;

namespace StockFlow.DataAccess.Products;

public interface ICategoryRepository
{
    IReadOnlyList<Category> GetAll();
    int Add(Category category);
}
