using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.Domain;
using StockFlow.UI.Services;

namespace StockFlow.UI.ViewModels;

public sealed partial class ProductsViewModel : ViewModelBase
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly IConfirmationService _confirmation;
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
    private string? _formErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(FormLabel), nameof(SaveButtonText))]
    private int? _editingProductId;

    public ProductsViewModel(
        ProductService productService,
        CategoryService categoryService,
        ICurrentUserContext currentUser,
        IConfirmationService confirmation)
    {
        ArgumentNullException.ThrowIfNull(productService);
        ArgumentNullException.ThrowIfNull(categoryService);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(confirmation);
        _productService = productService;
        _categoryService = categoryService;
        _confirmation = confirmation;
        CanManage = currentUser.IsAdmin;
    }

    public bool CanManage { get; }

    public bool IsEditing => EditingProductId is not null;

    public string FormLabel => IsEditing ? "Редагування:" : "Новий товар:";

    public string SaveButtonText => IsEditing ? "Зберегти" : "+ Додати";

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
    private async Task SaveAsync()
    {
        FormErrorMessage = null;
        try
        {
            var name = NewProductName;
            var categoryId = NewProductCategory?.Id;
            var unit = NewProductUnit?.Unit;
            var editingId = EditingProductId;
            if (editingId is null)
            {
                await Task.Run(() => _productService.Add(name, categoryId, unit));
            }
            else
            {
                await Task.Run(() => _productService.Update(editingId.Value, name, categoryId, unit));
                ClearForm();
            }

            NewProductName = string.Empty;
            EditingProductId = null;
        }
        catch (ValidationException error)
        {
            FormErrorMessage = error.Message;
            return;
        }
        catch (Exception error)
        {
            FormErrorMessage = DataErrorText.From(error);
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private void Edit(ProductRow row)
    {
        var product = _allProducts.FirstOrDefault(item => item.Id == row.Id);
        if (product is null)
        {
            return;
        }

        FormErrorMessage = null;
        EditingProductId = product.Id;
        NewProductName = product.Name;
        NewProductCategory = ProductCategories.FirstOrDefault(item => item.Id == product.CategoryId);
        NewProductUnit = Units.FirstOrDefault(option => option.Unit == product.Unit);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        FormErrorMessage = null;
        EditingProductId = null;
        ClearForm();
    }

    private void ClearForm()
    {
        NewProductName = string.Empty;
        NewProductCategory = null;
        NewProductUnit = null;
    }

    [RelayCommand]
    private async Task DeleteAsync(ProductRow row)
    {
        FormErrorMessage = null;
        if (!_confirmation.Confirm($"Видалити товар «{row.Name}»?"))
        {
            return;
        }

        try
        {
            await Task.Run(() => _productService.Delete(row.Id));
        }
        catch (ValidationException error)
        {
            FormErrorMessage = error.Message;
            return;
        }
        catch (Exception error)
        {
            FormErrorMessage = DataErrorText.From(error);
            return;
        }

        if (EditingProductId == row.Id)
        {
            CancelEdit();
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
                product.Id,
                product.Name,
                _categoryNames.GetValueOrDefault(product.CategoryId, string.Empty),
                UnitOfMeasureText.Short(product.Unit)))
            .ToList();
        IsEmpty = Products.Count == 0 && ErrorMessage is null;
    }
}
