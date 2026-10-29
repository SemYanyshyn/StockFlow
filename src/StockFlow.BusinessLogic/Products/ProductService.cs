using StockFlow.DataAccess.Products;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic.Products;

public sealed class ProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public IReadOnlyList<Product> GetAll() => _repository.GetAll();

    public static IReadOnlyList<Product> Filter(IEnumerable<Product> products, int? categoryId, string? query)
    {
        ArgumentNullException.ThrowIfNull(products);
        return products
            .Where(product => (categoryId is null || product.CategoryId == categoryId) && product.MatchesSearch(query))
            .ToList();
    }
}
