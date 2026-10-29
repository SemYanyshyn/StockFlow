using System;
using CommunityToolkit.Mvvm.ComponentModel;
using StockFlow.UI.ViewModels;

namespace StockFlow.UI.Services
{
    public class NavigationService : ObservableObject, INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private ViewModelBase _currentView = null!;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ViewModelBase CurrentView
        {
            get => _currentView;
            private set => SetProperty(ref _currentView, value);
        }

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        {
            var vm = _serviceProvider.GetService(typeof(TViewModel)) as ViewModelBase
                     ?? Activator.CreateInstance<TViewModel>();
            CurrentView = vm;
        }
    }
}