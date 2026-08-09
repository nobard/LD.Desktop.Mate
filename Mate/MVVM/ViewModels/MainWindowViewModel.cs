using System;
using System.Collections.Generic;
using System.Linq;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class MainWindowViewModel : BaseViewModel
{
    private string _currentToolTitle = string.Empty;
    private bool _isUpdateAvailable;
    private bool _isInstallingUpdate;
    private string _updateText = string.Empty;
    private Action? _installUpdateAction;

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
            new("文", "Переводчик", typeof(TranslatorViewModel)),
            new("●", "Уведомления", typeof(NotificationsViewModel)),
            new("◌", "Помодоро", typeof(PomodoroViewModel))
        };
        NavigateCommand = new DelegateCommand(Navigate);
        OpenUpdateCommand = new DelegateCommand(
            _ => _installUpdateAction?.Invoke(),
            _ => IsUpdateAvailable && !_isInstallingUpdate && _installUpdateAction is not null);
        Navigate(NavigationItems[0]);
    }

    public INavigationService NavigationService { get; }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public DelegateCommand NavigateCommand { get; }

    public DelegateCommand OpenUpdateCommand { get; }

    public string CurrentToolTitle
    {
        get => _currentToolTitle;
        private set => SetProperty(ref _currentToolTitle, value);
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => SetProperty(ref _isUpdateAvailable, value);
    }

    public string UpdateText
    {
        get => _updateText;
        private set => SetProperty(ref _updateText, value);
    }

    public void ShowUpdateAvailable(string version, Action installUpdate)
    {
        _installUpdateAction = installUpdate;
        _isInstallingUpdate = false;
        UpdateText = $"Доступна {version}";
        IsUpdateAvailable = true;
        OpenUpdateCommand.RaiseCanExecuteChanged();
    }

    public void SetUpdateInstallationInProgress()
    {
        if (!IsUpdateAvailable) return;

        _isInstallingUpdate = true;
        UpdateText = "Скачивание обновления…";
        OpenUpdateCommand.RaiseCanExecuteChanged();
    }

    public void ClearUpdate()
    {
        _installUpdateAction = null;
        _isInstallingUpdate = false;
        IsUpdateAvailable = false;
        UpdateText = string.Empty;
        OpenUpdateCommand.RaiseCanExecuteChanged();
    }

    public void NavigateTo<TViewModel>() where TViewModel : BaseViewModel
    {
        var item = NavigationItems.FirstOrDefault(candidate =>
            candidate.TargetViewModelType == typeof(TViewModel));
        if (item is not null) Navigate(item);
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
            case var type when type == typeof(NotificationsViewModel): NavigationService.NavigateTo<NotificationsViewModel>(); break;
            case var type when type == typeof(PomodoroViewModel): NavigationService.NavigateTo<PomodoroViewModel>(); break;
            default: return;
        }

        foreach (var navigationItem in NavigationItems)
        {
            navigationItem.IsSelected = ReferenceEquals(navigationItem, item);
        }

        CurrentToolTitle = item.ToolTip.ToUpperInvariant();
    }
}
