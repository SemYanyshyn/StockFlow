using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Suppliers;
using StockFlow.BusinessLogic.Users;
using StockFlow.Domain;

namespace StockFlow.UI.ViewModels;

public sealed class SuppliersViewModel : ViewModelBase
{
    private const string LoadErrorText = "Не вдалося завантажити постачальників. Перевірте з'єднання з базою даних.";
    private const string SaveErrorText = "Не вдалося зберегти постачальника. Перевірте підключення до бази даних.";

    private readonly SupplierService _service;
    private IReadOnlyList<Supplier> _allSuppliers = Array.Empty<Supplier>();
    private IReadOnlyList<Supplier> _suppliers = Array.Empty<Supplier>();
    private string _searchText = string.Empty;
    private string _newName = string.Empty;
    private string _newPhone = string.Empty;
    private string _newEmail = string.Empty;
    private string? _errorMessage;
    private bool _isBusy;
    private bool _isEditorOpen;
    private bool _isEmpty;

    public SuppliersViewModel(SupplierService service, ICurrentUserContext user)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(user);
        _service = service;
        CanManage = user.IsAdmin;

        ReloadCommand = new AsyncRelayCommand(LoadAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        OpenEditorCommand = new RelayCommand(OpenEditor);
        CancelEditorCommand = new RelayCommand(CancelEditor);
    }

    public bool CanManage { get; }

    public IAsyncRelayCommand ReloadCommand { get; }
    public IAsyncRelayCommand AddCommand { get; }
    public IRelayCommand OpenEditorCommand { get; }
    public IRelayCommand CancelEditorCommand { get; }

    public IReadOnlyList<Supplier> Suppliers
    {
        get => _suppliers;
        private set => SetProperty(ref _suppliers, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string NewName
    {
        get => _newName;
        set => SetProperty(ref _newName, value);
    }

    public string NewPhone
    {
        get => _newPhone;
        set => SetProperty(ref _newPhone, value);
    }

    public string NewEmail
    {
        get => _newEmail;
        set => SetProperty(ref _newEmail, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsEditorOpen
    {
        get => _isEditorOpen;
        private set => SetProperty(ref _isEditorOpen, value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            _allSuppliers = await Task.Run(_service.GetAll);
        }
        catch (Exception)
        {
            _allSuppliers = Array.Empty<Supplier>();
            ErrorMessage = LoadErrorText;
        }
        finally
        {
            IsBusy = false;
            ApplyFilter();
        }
    }

    private void OpenEditor()
    {
        if (!CanManage || IsBusy) return;
        ErrorMessage = null;
        IsEditorOpen = true;
    }

    private void CancelEditor()
    {
        IsEditorOpen = false;
        ErrorMessage = null;
        NewName = string.Empty;
        NewPhone = string.Empty;
        NewEmail = string.Empty;
    }

    private async Task AddAsync()
    {
        if (!CanManage)
        {
            ErrorMessage = SupplierService.AdminOnlyMessage;
            return;
        }
        if (IsBusy || !IsEditorOpen) return;

        IsBusy = true;
        ErrorMessage = null;
        Supplier added;
        try
        {
            var name = NewName;
            var phone = NewPhone;
            var email = NewEmail;
            added = await Task.Run(() => _service.Add(name, phone, email));
        }
        catch (ValidationException error)
        {
            ErrorMessage = error.Message;
            IsBusy = false;
            return;
        }
        catch (Exception)
        {
            ErrorMessage = SaveErrorText;
            IsBusy = false;
            return;
        }

        CancelEditor();
        try
        {
            _allSuppliers = await Task.Run(_service.GetAll);
        }
        catch (Exception)
        {
            // The INSERT succeeded. Keep the new record visible even if refresh fails.
            _allSuppliers = _allSuppliers.Append(added).OrderBy(s => s.Name).ToArray();
            ErrorMessage = "Постачальника додано, але оновити список з бази не вдалося.";
        }
        finally
        {
            IsBusy = false;
            SearchText = string.Empty;
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        Suppliers = SupplierService.Filter(_allSuppliers, SearchText);
        IsEmpty = !IsBusy && Suppliers.Count == 0 && ErrorMessage is null;
    }
}
