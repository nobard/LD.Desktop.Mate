using System;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.Services.Implementations;

public sealed class NavigationService : ObservableObject, INavigationService
{
    private readonly Func<Type, BaseViewModel> _viewModelFactory;
    private BaseViewModel? _currentView;

    public NavigationService(Func<Type, BaseViewModel> viewModelFactory) => _viewModelFactory = viewModelFactory;

    public BaseViewModel? CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    public void NavigateTo<T>() where T : BaseViewModel => CurrentView = _viewModelFactory(typeof(T));

    public void NavigateTo<T>(Action<T> action) where T : BaseViewModel
    {
        NavigateTo<T>();
        action((T)CurrentView!);
    }
}
