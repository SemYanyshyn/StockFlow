using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic.Products;
using StockFlow.Domain;

namespace StockFlow.UI.Products;

public sealed partial class ProductsViewModel : ObservableObject
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private IReadOnlyList<Product> _allProducts = Array.Empty<Product>();
    private Dictionary<int, string> _categoryNames = new();

    [ObservableProperty]
    private IReadOnlyList<ProductRow> _products = Array.Empty<ProductRow>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CategoryFilterItem? _selectedCategory = CategoryFilterItem.All;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string? _errorMessage;

    public ProductsViewModel(ProductService productService, CategoryService categoryService)
    {
        ArgumentNullException.ThrowIfNull(productService);
        ArgumentNullException.ThrowIfNull(categoryService);
        _productService = productService;
        _categoryService = categoryService;
    }

    public ObservableCollection<CategoryFilterItem> Categories { get; } = new() { CategoryFilterItem.All };

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCategoryChanged(CategoryFilterItem? value) => ApplyFilter();

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        try
        {
            var (products, categories) = await Task.Run(() => (_productService.GetAll(), _categoryService.GetAll()));
            _allProducts = products;
            _categoryNames = categories.ToDictionary(category => category.Id, category => category.Name);
            RefreshCategories(categories);
        }
        catch (Exception error)
        {
            _allProducts = Array.Empty<Product>();
            ErrorMessage = DataErrorText.From(error);
        }

        ApplyFilter();
    }

    private void RefreshCategories(IEnumerable<Category> categories)
    {
        var selectedId = SelectedCategory?.Id;
        Categories.Clear();
        Categories.Add(CategoryFilterItem.All);
        foreach (var category in categories)
        {
            Categories.Add(new CategoryFilterItem(category.Id, category.Name));
        }

        SelectedCategory = Categories.FirstOrDefault(item => item.Id == selectedId) ?? CategoryFilterItem.All;
    }

    private void ApplyFilter()
    {
        Products = ProductService.Filter(_allProducts, SelectedCategory?.Id, SearchText)
            .Select(product => new ProductRow(
                product.Name,
                _categoryNames.GetValueOrDefault(product.CategoryId, string.Empty),
                UnitOfMeasureText.Short(product.Unit)))
            .ToList();
        IsEmpty = Products.Count == 0 && ErrorMessage is null;
    }
}
