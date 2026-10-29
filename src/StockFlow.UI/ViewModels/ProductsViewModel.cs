using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.Domain;

namespace StockFlow.UI.ViewModels;

public sealed partial class ProductsViewModel : ViewModelBase
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

    [ObservableProperty]
    private string _newProductName = string.Empty;

    [ObservableProperty]
    private CategoryFilterItem? _newProductCategory;

    [ObservableProperty]
    private UnitOption? _newProductUnit;

    [ObservableProperty]
    private string? _addErrorMessage;

    public ProductsViewModel(ProductService productService, CategoryService categoryService, ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(productService);
        ArgumentNullException.ThrowIfNull(categoryService);
        ArgumentNullException.ThrowIfNull(currentUser);
        _productService = productService;
        _categoryService = categoryService;
        CanManage = currentUser.IsAdmin;
    }

    public bool CanManage { get; }

    public ObservableCollection<CategoryFilterItem> Categories { get; } = new() { CategoryFilterItem.All };

    public ObservableCollection<CategoryFilterItem> ProductCategories { get; } = new();

    public IReadOnlyList<UnitOption> Units => UnitOption.All;

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

    [RelayCommand]
    private async Task AddAsync()
    {
        AddErrorMessage = null;
        try
        {
            var name = NewProductName;
            var categoryId = NewProductCategory?.Id;
            var unit = NewProductUnit?.Unit;
            await Task.Run(() => _productService.Add(name, categoryId, unit));
            NewProductName = string.Empty;
        }
        catch (ValidationException error)
        {
            AddErrorMessage = error.Message;
            return;
        }
        catch (Exception error)
        {
            AddErrorMessage = DataErrorText.From(error);
            return;
        }

        await LoadAsync();
    }

    private void RefreshCategories(IEnumerable<Category> categories)
    {
        var selectedId = SelectedCategory?.Id;
        var newProductCategoryId = NewProductCategory?.Id;
        Categories.Clear();
        ProductCategories.Clear();
        Categories.Add(CategoryFilterItem.All);
        foreach (var category in categories)
        {
            var item = new CategoryFilterItem(category.Id, category.Name);
            Categories.Add(item);
            ProductCategories.Add(item);
        }

        SelectedCategory = Categories.FirstOrDefault(item => item.Id == selectedId) ?? CategoryFilterItem.All;
        NewProductCategory = ProductCategories.FirstOrDefault(item => item.Id == newProductCategoryId);
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
