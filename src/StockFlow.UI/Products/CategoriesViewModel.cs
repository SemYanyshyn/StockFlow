using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic.Products;
using StockFlow.Domain;

namespace StockFlow.UI.Products;

public sealed partial class CategoriesViewModel : ObservableObject
{
    private readonly CategoryService _categoryService;

    [ObservableProperty]
    private IReadOnlyList<Category> _categories = Array.Empty<Category>();

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _canManage;

    public CategoriesViewModel(CategoryService categoryService)
    {
        ArgumentNullException.ThrowIfNull(categoryService);
        _categoryService = categoryService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        try
        {
            Categories = await Task.Run(() => _categoryService.GetAll());
        }
        catch (Exception error)
        {
            ErrorMessage = DataErrorText.From(error);
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        ErrorMessage = null;
        try
        {
            var name = NewCategoryName;
            await Task.Run(() => _categoryService.Add(name));
            NewCategoryName = string.Empty;
        }
        catch (ValidationException error)
        {
            ErrorMessage = error.Message;
            return;
        }
        catch (Exception error)
        {
            ErrorMessage = DataErrorText.From(error);
            return;
        }

        await LoadAsync();
    }
}
