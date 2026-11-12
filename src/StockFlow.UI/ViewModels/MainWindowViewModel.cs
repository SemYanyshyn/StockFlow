using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StockFlow.BusinessLogic.Users;
using StockFlow.UI.Services;

namespace StockFlow.UI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public INavigationService Navigation { get; }
        private readonly ICurrentUserContext _currentUser;
        public bool IsAdmin => _currentUser.IsAdmin;

        public IRelayCommand NavigateProductsCommand { get; }
        public IRelayCommand NavigateCategoriesCommand { get; }
        public IRelayCommand NavigateSuppliersCommand { get; }
        public IRelayCommand NavigateMovementsCommand { get; }
        public IRelayCommand NavigateStockCommand { get; }
        public IRelayCommand NavigateWarehousesCommand { get; }
        public IRelayCommand NavigateReportsCommand { get; }
        public IRelayCommand NavigateUsersCommand { get; }

        public MainWindowViewModel(INavigationService navigation, ICurrentUserContext currentUser)
        {
            Navigation = navigation;
            _currentUser = currentUser;
            if (currentUser is AuthenticatedUserContext context)
                context.UserChanged += () => OnPropertyChanged(nameof(IsAdmin));

            NavigateProductsCommand = new RelayCommand(() => Navigation.NavigateTo<ProductsViewModel>());
            NavigateCategoriesCommand = new RelayCommand(() => Navigation.NavigateTo<CategoriesViewModel>());
            NavigateSuppliersCommand = new RelayCommand(() => Navigation.NavigateTo<SuppliersViewModel>());
            NavigateMovementsCommand = new RelayCommand(() => Navigation.NavigateTo<StockMovementsViewModel>());
            NavigateStockCommand = new RelayCommand(() => Navigation.NavigateTo<StockBalancesViewModel>());
            NavigateWarehousesCommand = new RelayCommand(() => Navigation.NavigateTo<WarehousesViewModel>());
            NavigateReportsCommand = new RelayCommand(() => Navigation.NavigateTo<ReportsViewModel>());
            NavigateUsersCommand = new RelayCommand(() => { if (IsAdmin) Navigation.NavigateTo<UsersViewModel>(); });

            Navigation.NavigateTo<WarehousesViewModel>();
        }
    }
}
