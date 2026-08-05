using System;
using Mate.MVVM.Core;

namespace Mate.Services.Interfaces;

public interface INavigationService
{
    BaseViewModel? CurrentView { get; }

    void NavigateTo<T>() where T : BaseViewModel;

    void NavigateTo<T>(Action<T> action) where T : BaseViewModel;
}
