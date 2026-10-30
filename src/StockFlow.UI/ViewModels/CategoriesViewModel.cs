using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.Domain;
using StockFlow.UI.Services;

namespace StockFlow.UI.ViewModels;

public sealed partial class CategoriesViewModel : ViewModelBase
{
    private readonly CategoryService _categoryService;
    private readonly IConfirmationService _confirmation;

    [ObservableProperty]
    private IReadOnlyList<Category> _categories = Array.Empty<Category>();

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(FormLabel), nameof(SaveButtonText))]
    private Category? _editingCategory;

    public CategoriesViewModel(CategoryService categoryService, ICurrentUserContext currentUser, IConfirmationService confirmation)
    {
        ArgumentNullException.ThrowIfNull(categoryService);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(confirmation);
        _categoryService = categoryService;
        _confirmation = confirmation;
        CanManage = currentUser.IsAdmin;
    }

    public bool CanManage { get; }

    public bool IsEditing => EditingCategory is not null;

    public string FormLabel => IsEditing ? "Нова назва:" : "Назва категорії:";

    public string SaveButtonText => IsEditing ? "Зберегти" : "+ Додати категорію";

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
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        try
        {
            var name = NewCategoryName;
            var editing = EditingCategory;
            if (editing is null)
            {
                await Task.Run(() => _categoryService.Add(name));
            }
            else
            {
                await Task.Run(() => _categoryService.Update(editing.Id, name));
            }

            NewCategoryName = string.Empty;
            EditingCategory = null;
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

    [RelayCommand]
    private void Edit(Category category)
    {
        ErrorMessage = null;
        EditingCategory = category;
        NewCategoryName = category.Name;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ErrorMessage = null;
        EditingCategory = null;
        NewCategoryName = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteAsync(Category category)
    {
        ErrorMessage = null;
        if (!_confirmation.Confirm($"Видалити категорію «{category.Name}»?"))
        {
            return;
        }

        try
        {
            await Task.Run(() => _categoryService.Delete(category.Id));
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

        if (EditingCategory?.Id == category.Id)
        {
            CancelEdit();
        }

        await LoadAsync();
    }
}
