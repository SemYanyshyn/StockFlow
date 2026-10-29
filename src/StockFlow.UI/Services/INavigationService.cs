using StockFlow.UI.ViewModels;

namespace StockFlow.UI.Services
{
    public interface INavigationService
    {
        ViewModelBase? CurrentView { get; }
        void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
    }
}
