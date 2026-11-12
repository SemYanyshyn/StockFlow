using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic.Products;
using StockFlow.BusinessLogic.Users;
using StockFlow.Domain;
using StockFlow.UI.Services;

namespace StockFlow.UI.ViewModels;

public sealed partial class UsersViewModel : ViewModelBase
{
    private readonly UserService _service;
    private readonly IConfirmationService _confirmation;
    private readonly ICurrentUserContext _current;
    private IReadOnlyList<User> _all = Array.Empty<User>();
    private int? _editingId;
    public event Action? FormReset;
    public IReadOnlyList<RoleOption> Roles { get; } = new[]
    {
        new RoleOption("Працівник", UserRole.Employee), new RoleOption("Адміністратор", UserRole.Admin)
    };
    public IReadOnlyList<RoleOption> FilterRoles { get; }
    public bool CanManage => _current.IsAdmin;
    public bool IsCreating => !_editingId.HasValue;
    public bool IsEditing => _editingId.HasValue;
    public string EditorTitle => IsCreating ? "Новий користувач" : "Редагування користувача";
    public bool IsIdle => !IsBusy;
    public bool IsEmpty => !IsBusy && ErrorMessage is null && Users.Count == 0;
    [ObservableProperty] private IReadOnlyList<UserRow> _users = Array.Empty<UserRow>();
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private RoleOption _filterRole;
    [ObservableProperty] private RoleOption _editorRole;
    [ObservableProperty] private string _editorUsername = string.Empty;
    [ObservableProperty] private string _editorFullName = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(IsEmpty))]
    private bool _isBusy;

    public UsersViewModel(UserService service, ICurrentUserContext current, IConfirmationService confirmation)
    {
        _service = service;
        _current = current;
        _confirmation = confirmation;
        FilterRoles = new[] { new RoleOption("Усі ролі", null) }.Concat(Roles).ToArray();
        _filterRole = FilterRoles[0];
        _editorRole = Roles[0];
    }
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFilterRoleChanged(RoleOption value) => ApplyFilter();
    private void ApplyFilter()
    {
        try { Users = _service.Filter(_all, SearchText, FilterRole.Value).Select(u => new UserRow(u)).ToArray(); }
        catch (ValidationException error) { Users = Array.Empty<UserRow>(); ErrorMessage = error.Message; }
        OnPropertyChanged(nameof(IsEmpty));
    }
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try { _all = await Task.Run(_service.GetAll); }
        catch (ValidationException error) { _all = Array.Empty<User>(); ErrorMessage = error.Message; }
        catch (Exception) { _all = Array.Empty<User>(); ErrorMessage = DataErrorText.ConnectionFailed; }
        finally { IsBusy = false; ApplyFilter(); }
    }
    [RelayCommand] private Task ReloadAsync() => LoadAsync();
    [RelayCommand]
    private void Create()
    {
        if (IsBusy || !CanManage) return;
        Reset();
        IsEditorOpen = true;
    }
    [RelayCommand]
    private void Edit(UserRow? row)
    {
        if (IsBusy || !CanManage || row is null) return;
        Reset();
        _editingId = row.Id;
        EditorUsername = row.Username;
        EditorFullName = row.FullName;
        EditorRole = Roles.Single(r => r.Value == row.User.Role);
        NotifyEditor();
        IsEditorOpen = true;
    }
    private void NotifyEditor()
    {
        OnPropertyChanged(nameof(IsCreating));
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(EditorTitle));
    }
    [RelayCommand] private void Cancel() { if (!IsBusy) Reset(); }
    private void Reset()
    {
        _editingId = null;
        IsEditorOpen = false;
        EditorUsername = EditorFullName = string.Empty;
        EditorRole = Roles[0];
        ErrorMessage = null;
        NotifyEditor();
        FormReset?.Invoke();
    }
    [RelayCommand]
    private async Task SaveAsync(string? password)
    {
        if (IsBusy || !CanManage || !IsEditorOpen) return;
        var id = _editingId;
        var username = EditorUsername;
        var name = EditorFullName;
        var role = EditorRole.Value ?? UserRole.Employee;
        await MutateAsync(() =>
        {
            var result = id.HasValue ? _service.Update(id.Value, name, role) : _service.Create(username, name, password, role);
            return _all.Where(u => u.Id != result.Id).Append(result).OrderBy(u => u.Username).ToArray();
        }, closeEditor: true);
    }
    [RelayCommand]
    private async Task ToggleAsync(UserRow? row)
    {
        if (row is null || IsBusy || !CanManage) return;
        await MutateAsync(() =>
        {
            _service.SetActive(row.Id, !row.User.IsActive);
            return _all.Select(u => u.Id == row.Id ? new User
            {
                Id = u.Id, Username = u.Username, FullName = u.FullName, Role = u.Role, IsActive = !u.IsActive
            } : u).ToArray();
        });
    }
    [RelayCommand]
    private async Task DeleteAsync(UserRow? row)
    {
        if (row is null || IsBusy || !CanManage) return;
        if (!_confirmation.Confirm($"Видалити користувача «{row.Username}»?")) return;
        await MutateAsync(() =>
        {
            _service.Delete(row.Id);
            return _all.Where(u => u.Id != row.Id).ToArray();
        }, closeEditor: _editingId == row.Id);
    }
    private async Task MutateAsync(Func<IReadOnlyList<User>> action, bool closeEditor = false)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var fallback = await Task.Run(action);
            if (closeEditor) Reset();
            _all = fallback;
            try { _all = await Task.Run(_service.GetAll); }
            catch (ValidationException error) { _all = Array.Empty<User>(); ErrorMessage = error.Message; }
            catch (Exception) { ErrorMessage = "Зміни збережено, але оновити список не вдалося. Натисніть «Оновити»."; }
        }
        catch (ValidationException error) { ErrorMessage = error.Message; }
        catch (Exception) { ErrorMessage = DataErrorText.ConnectionFailed; }
        finally { IsBusy = false; ApplyFilter(); OnPropertyChanged(nameof(CanManage)); }
    }
}
