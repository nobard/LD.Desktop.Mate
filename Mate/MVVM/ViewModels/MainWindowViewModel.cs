using System;
using System.Collections.Generic;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class MainWindowViewModel : BaseViewModel
{
    private string _currentToolTitle = string.Empty;

    public MainWindowViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;
        NavigationItems = new List<NavigationItemViewModel>
        {
            new("♪", "Плеер", typeof(MusicViewModel)),
            new("□", "Папка", typeof(FolderViewModel)),
            new("▣", "Буфер обмена", typeof(ClipboardViewModel)),
            new("⚑", "Заготовки", typeof(SnippetsViewModel)),
            new("◉", "Инкогнито", typeof(IncognitoViewModel)),
            new("文", "Переводчик", typeof(TranslatorViewModel))
        };
        NavigateCommand = new DelegateCommand(Navigate);
        Navigate(NavigationItems[0]);
    }

    public INavigationService NavigationService { get; }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public DelegateCommand NavigateCommand { get; }

    public string CurrentToolTitle
    {
        get => _currentToolTitle;
        private set => SetProperty(ref _currentToolTitle, value);
    }

    private void Navigate(object? parameter)
    {
        if (parameter is not NavigationItemViewModel item) return;

        switch (item.TargetViewModelType)
        {
            case var type when type == typeof(MusicViewModel): NavigationService.NavigateTo<MusicViewModel>(); break;
            case var type when type == typeof(FolderViewModel): NavigationService.NavigateTo<FolderViewModel>(); break;
            case var type when type == typeof(ClipboardViewModel): NavigationService.NavigateTo<ClipboardViewModel>(); break;
            case var type when type == typeof(SnippetsViewModel): NavigationService.NavigateTo<SnippetsViewModel>(); break;
            case var type when type == typeof(IncognitoViewModel): NavigationService.NavigateTo<IncognitoViewModel>(); break;
            case var type when type == typeof(TranslatorViewModel): NavigationService.NavigateTo<TranslatorViewModel>(); break;
            default: return;
        }

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = ReferenceEquals(navigationItem, item);
        }

        CurrentToolTitle = item.ToolTip.ToUpperInvariant();
    }
}
