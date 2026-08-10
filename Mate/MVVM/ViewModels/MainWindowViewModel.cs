using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class MainWindowViewModel : BaseViewModel, IDisposable
{
    private readonly IPrivateBrowserService _browserService;
    private readonly IFeatureLayoutService _featureLayoutService;
    private readonly NavigationItemViewModel _browserNavigationItem;
    private readonly IReadOnlyDictionary<AppFeature, NavigationItemViewModel> _navigationItemsByFeature;
    private string _currentToolTitle = string.Empty;
    private bool _isUpdateAvailable;
    private bool _isInstallingUpdate;
    private string _updateText = string.Empty;
    private Action? _installUpdateAction;

    public MainWindowViewModel(
        INavigationService navigationService,
        IPrivateBrowserService browserService,
        IFeatureLayoutService featureLayoutService)
    {
        NavigationService = navigationService;
        _browserService = browserService;
        _featureLayoutService = featureLayoutService;
        _browserNavigationItem = new("◉", "Браузер", typeof(IncognitoViewModel))
        {
            UsePrivateBrowserIcon = _browserService.Settings.UsePrivateMode
        };
        _navigationItemsByFeature = new Dictionary<AppFeature, NavigationItemViewModel>
        {
            [AppFeature.Player] = new("♪", "Плеер", typeof(MusicViewModel)),
            [AppFeature.Folder] = new("□", "Папка", typeof(FolderViewModel)),
            [AppFeature.Clipboard] = new("▣", "Буфер обмена", typeof(ClipboardViewModel)),
            [AppFeature.Snippets] = new("⚑", "Заготовки", typeof(SnippetsViewModel)),
            [AppFeature.Browser] = _browserNavigationItem,
            [AppFeature.Translator] = new("文", "Переводчик", typeof(TranslatorViewModel)),
            [AppFeature.Notifications] = new("●", "Уведомления", typeof(NotificationsViewModel)),
            [AppFeature.Pomodoro] = new("◌", "Помодоро", typeof(PomodoroViewModel))
        };
        NavigationItems = new ObservableCollection<NavigationItemViewModel>();
        NavigateCommand = new DelegateCommand(Navigate);
        OpenUpdateCommand = new DelegateCommand(
            _ => _installUpdateAction?.Invoke(),
            _ => IsUpdateAvailable && !_isInstallingUpdate && _installUpdateAction is not null);
        _browserService.SettingsChanged += BrowserService_SettingsChanged;
        _featureLayoutService.LayoutChanged += FeatureLayoutService_LayoutChanged;
        ApplyFeatureLayout();
    }

    public INavigationService NavigationService { get; }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

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
        if (parameter is not NavigationItemViewModel item || !NavigationItems.Contains(item)) return;

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

    private void BrowserService_SettingsChanged(object? sender, EventArgs e) =>
        _browserNavigationItem.UsePrivateBrowserIcon = _browserService.Settings.UsePrivateMode;

    private void FeatureLayoutService_LayoutChanged(object? sender, EventArgs e) => ApplyFeatureLayout();

    private void ApplyFeatureLayout()
    {
        var selectedItem = NavigationItems.FirstOrDefault(item => item.IsSelected);
        NavigationItems.Clear();

        foreach (var layoutItem in _featureLayoutService.Items)
        {
            if (layoutItem.IsVisible
                && _navigationItemsByFeature.TryGetValue(layoutItem.Feature, out var navigationItem))
            {
                NavigationItems.Add(navigationItem);
            }
        }

        var selectedItemRemainsVisible = selectedItem is not null && NavigationItems.Contains(selectedItem);
        foreach (var navigationItem in _navigationItemsByFeature.Values)
        {
            navigationItem.IsSelected = selectedItemRemainsVisible
                                        && ReferenceEquals(navigationItem, selectedItem);
        }

        if (selectedItemRemainsVisible) return;
        if (NavigationItems.Count > 0) Navigate(NavigationItems[0]);
    }

    public void Dispose()
    {
        _browserService.SettingsChanged -= BrowserService_SettingsChanged;
        _featureLayoutService.LayoutChanged -= FeatureLayoutService_LayoutChanged;
    }
}
