using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IThemeService _themeService;
    private readonly IAutoStartService _autoStartService;
    private readonly IHoverActivationService _hoverActivationService;
    private readonly IUpdateService _updateService;
    private readonly IPrivateBrowserService _browserService;
    private readonly IFileShelfService _fileShelfService;
    private readonly IFeatureLayoutService _featureLayoutService;
    private bool _isAutoStartEnabled;
    private bool _isHoverActivationDisabled;
    private BrowserOption? _selectedBrowser;
    private SearchEngineOption? _selectedSearchEngine;
    private bool _isPrivateBrowserEnabled;
    private string _storageFolder = string.Empty;
    private AppTheme _currentTheme;
    private string _errorMessage = string.Empty;
    private string _updateStatusText;
    private string _updateActionText = "Проверить";
    private bool _isUpdateActionEnabled = true;
    private Action? _installUpdateAction;
    private bool _isRefreshing;

    public SettingsViewModel(
        IThemeService themeService,
        IAutoStartService autoStartService,
        IHoverActivationService hoverActivationService,
        IUpdateService updateService,
        IPrivateBrowserService browserService,
        IFileShelfService fileShelfService,
        IFeatureLayoutService featureLayoutService)
    {
        _themeService = themeService;
        _autoStartService = autoStartService;
        _hoverActivationService = hoverActivationService;
        _updateService = updateService;
        _browserService = browserService;
        _fileShelfService = fileShelfService;
        _featureLayoutService = featureLayoutService;
        _themeService.ThemeChanged += ThemeService_ThemeChanged;
        _autoStartService.EnabledChanged += AutoStartService_EnabledChanged;
        _hoverActivationService.EnabledChanged += HoverActivationService_EnabledChanged;
        _browserService.SettingsChanged += BrowserService_SettingsChanged;
        _fileShelfService.StorageFolderChanged += FileShelfService_StorageFolderChanged;
        _featureLayoutService.LayoutChanged += FeatureLayoutService_LayoutChanged;

        SelectThemeCommand = new DelegateCommand(SelectTheme);
        ToggleFeatureCommand = new DelegateCommand(ToggleFeature, CanToggleFeature);
        UpdateActionCommand = new DelegateCommand(
            _ => ExecuteUpdateAction(),
            _ => IsUpdateActionEnabled);
        ChooseStorageFolderCommand = new DelegateCommand(
            _ => ChooseStorageFolderRequested?.Invoke(this, EventArgs.Empty));
        CloseCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
        _updateStatusText = $"Установлена версия {_updateService.CurrentVersion}";
        Features = new ObservableCollection<FeatureOptionViewModel>();
        Refresh();
    }

    public event EventHandler? CloseRequested;

    public event EventHandler? CheckForUpdatesRequested;

    public event EventHandler? ChooseStorageFolderRequested;

    public DelegateCommand SelectThemeCommand { get; }

    public DelegateCommand ToggleFeatureCommand { get; }

    public DelegateCommand UpdateActionCommand { get; }

    public DelegateCommand ChooseStorageFolderCommand { get; }

    public DelegateCommand CloseCommand { get; }

    public ObservableCollection<FeatureOptionViewModel> Features { get; }

    public bool IsDarkThemeSelected => _currentTheme == AppTheme.Dark;

    public bool IsBlackThemeSelected => _currentTheme == AppTheme.AlmostBlack;

    public IReadOnlyList<BrowserOption> AvailableBrowsers => _browserService.AvailableBrowsers;

    public IReadOnlyList<SearchEngineOption> AvailableSearchEngines => _browserService.AvailableSearchEngines;

    public BrowserOption? SelectedBrowser
    {
        get => _selectedBrowser;
        set
        {
            if (!SetProperty(ref _selectedBrowser, value)
                || _isRefreshing
                || value is null)
            {
                return;
            }

            _browserService.SetBrowser(value.Id);
        }
    }

    public SearchEngineOption? SelectedSearchEngine
    {
        get => _selectedSearchEngine;
        set
        {
            if (!SetProperty(ref _selectedSearchEngine, value)
                || _isRefreshing
                || value is null)
            {
                return;
            }

            _browserService.SetSearchEngine(value.Engine);
        }
    }

    public bool IsPrivateBrowserEnabled
    {
        get => _isPrivateBrowserEnabled;
        set
        {
            if (!SetProperty(ref _isPrivateBrowserEnabled, value) || _isRefreshing) return;
            _browserService.SetPrivateMode(value);
        }
    }

    public string StorageFolder
    {
        get => _storageFolder;
        private set => SetProperty(ref _storageFolder, value);
    }

    public bool IsAutoStartEnabled
    {
        get => _isAutoStartEnabled;
        set
        {
            if (_isRefreshing || value == _isAutoStartEnabled)
            {
                SetProperty(ref _isAutoStartEnabled, value);
                return;
            }

            if (_autoStartService.SetEnabled(value))
            {
                SetProperty(ref _isAutoStartEnabled, value);
                ErrorMessage = string.Empty;
                return;
            }

            SetProperty(ref _isAutoStartEnabled, _autoStartService.IsEnabled);
            ErrorMessage = "Не удалось изменить настройку автозапуска";
        }
    }

    public bool IsHoverActivationDisabled
    {
        get => _isHoverActivationDisabled;
        set
        {
            if (_isRefreshing || value == _isHoverActivationDisabled)
            {
                SetProperty(ref _isHoverActivationDisabled, value);
                return;
            }

            _hoverActivationService.SetEnabled(!value);
            SetProperty(ref _isHoverActivationDisabled, !_hoverActivationService.IsEnabled);
            ErrorMessage = string.Empty;
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value)) return;
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string VersionText => $"Mate {_updateService.CurrentVersion}";

    public string UpdateStatusText
    {
        get => _updateStatusText;
        private set => SetProperty(ref _updateStatusText, value);
    }

    public string UpdateActionText
    {
        get => _updateActionText;
        private set => SetProperty(ref _updateActionText, value);
    }

    public bool IsUpdateActionEnabled
    {
        get => _isUpdateActionEnabled;
        private set
        {
            if (!SetProperty(ref _isUpdateActionEnabled, value)) return;
            UpdateActionCommand.RaiseCanExecuteChanged();
        }
    }

    public void SetUpdateCheckInProgress(bool isInProgress)
    {
        if (isInProgress)
        {
            _installUpdateAction = null;
            UpdateStatusText = "Проверяем наличие новой версии…";
            UpdateActionText = "Проверка…";
            IsUpdateActionEnabled = false;
            return;
        }

        if (_installUpdateAction is null) UpdateActionText = "Проверить";
        IsUpdateActionEnabled = true;
    }

    public void ShowUpdateAvailable(string version, Action installUpdate)
    {
        _installUpdateAction = installUpdate;
        UpdateStatusText = $"Доступна версия {version}";
        UpdateActionText = "Обновить";
        IsUpdateActionEnabled = true;
    }

    public void ShowUpdateCheckMessage(string message)
    {
        _installUpdateAction = null;
        UpdateStatusText = message;
        UpdateActionText = "Проверить";
        IsUpdateActionEnabled = true;
    }

    public void SetUpdateInstallationInProgress()
    {
        UpdateStatusText = "Скачиваем и устанавливаем обновление…";
        UpdateActionText = "Установка…";
        IsUpdateActionEnabled = false;
    }

    public void Refresh()
    {
        _isRefreshing = true;
        try
        {
            _currentTheme = _themeService.CurrentTheme;
            IsAutoStartEnabled = _autoStartService.IsEnabled;
            IsHoverActivationDisabled = !_hoverActivationService.IsEnabled;
            RefreshBrowserSettings();
            RefreshFeatureLayout();
            StorageFolder = _fileShelfService.StorageFolder;
            ErrorMessage = string.Empty;
            RaiseThemeProperties();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void SelectTheme(object? parameter)
    {
        if (parameter is AppTheme theme) _themeService.SetTheme(theme);
    }

    private void ToggleFeature(object? parameter)
    {
        if (parameter is not FeatureOptionViewModel option) return;
        _featureLayoutService.SetVisible(option.Feature, !option.IsVisible);
    }

    private bool CanToggleFeature(object? parameter) =>
        parameter is FeatureOptionViewModel { CanToggleVisibility: true };

    public void MoveFeature(AppFeature feature, AppFeature targetFeature)
    {
        if (feature == targetFeature) return;

        var targetIndex = -1;
        for (var index = 0; index < _featureLayoutService.Items.Count; index++)
        {
            if (_featureLayoutService.Items[index].Feature == targetFeature)
            {
                targetIndex = index;
                break;
            }
        }

        if (targetIndex >= 0) _featureLayoutService.Move(feature, targetIndex);
    }

    private void ExecuteUpdateAction()
    {
        if (_installUpdateAction is not null)
        {
            _installUpdateAction();
            return;
        }

        CheckForUpdatesRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ThemeService_ThemeChanged(object? sender, EventArgs e)
    {
        RunOnUiThread(() =>
        {
            _currentTheme = _themeService.CurrentTheme;
            RaiseThemeProperties();
        });
    }

    private void HoverActivationService_EnabledChanged(object? sender, EventArgs e) =>
        RunOnUiThread(() =>
            SetProperty(ref _isHoverActivationDisabled, !_hoverActivationService.IsEnabled, nameof(IsHoverActivationDisabled)));

    private void AutoStartService_EnabledChanged(object? sender, EventArgs e) =>
        RunOnUiThread(() =>
            SetProperty(ref _isAutoStartEnabled, _autoStartService.IsEnabled, nameof(IsAutoStartEnabled)));

    private void BrowserService_SettingsChanged(object? sender, EventArgs e) =>
        RunOnUiThread(RefreshBrowserSettings);

    private void FileShelfService_StorageFolderChanged() =>
        RunOnUiThread(() => StorageFolder = _fileShelfService.StorageFolder);

    private void FeatureLayoutService_LayoutChanged(object? sender, EventArgs e) =>
        RunOnUiThread(RefreshFeatureLayout);

    public void SetStorageFolder(string folderPath)
    {
        if (_fileShelfService.SetStorageFolder(folderPath))
        {
            StorageFolder = _fileShelfService.StorageFolder;
            ErrorMessage = string.Empty;
            return;
        }

        ErrorMessage = "Не удалось использовать выбранную папку";
    }

    private void RefreshBrowserSettings()
    {
        var wasRefreshing = _isRefreshing;
        _isRefreshing = true;
        try
        {
            SelectedBrowser = FindBrowser(_browserService.Settings.BrowserId);
            SelectedSearchEngine = FindSearchEngine(_browserService.Settings.SearchEngine);
            IsPrivateBrowserEnabled = _browserService.Settings.UsePrivateMode;
        }
        finally
        {
            _isRefreshing = wasRefreshing;
        }
    }

    private void RefreshFeatureLayout()
    {
        Features.Clear();
        var visibleCount = 0;
        foreach (var item in _featureLayoutService.Items)
        {
            if (item.IsVisible) visibleCount++;
        }

        for (var index = 0; index < _featureLayoutService.Items.Count; index++)
        {
            var item = _featureLayoutService.Items[index];
            Features.Add(new FeatureOptionViewModel(
                item.Feature,
                GetFeatureDisplayName(item.Feature),
                item.IsVisible,
                !item.IsVisible || visibleCount > 1));
        }

        ToggleFeatureCommand.RaiseCanExecuteChanged();
    }

    private static string GetFeatureDisplayName(AppFeature feature) => feature switch
    {
        AppFeature.Player => "Плеер",
        AppFeature.Folder => "Папка",
        AppFeature.Clipboard => "Буфер обмена",
        AppFeature.Snippets => "Заготовки",
        AppFeature.Browser => "Браузер",
        AppFeature.Translator => "Переводчик",
        AppFeature.Notifications => "Уведомления",
        AppFeature.Pomodoro => "Помодоро",
        _ => feature.ToString()
    };

    private BrowserOption? FindBrowser(string browserId)
    {
        foreach (var browser in AvailableBrowsers)
        {
            if (browser.Id == browserId) return browser;
        }

        return AvailableBrowsers.Count > 0 ? AvailableBrowsers[0] : null;
    }

    private SearchEngineOption? FindSearchEngine(BrowserSearchEngine searchEngine)
    {
        foreach (var option in AvailableSearchEngines)
        {
            if (option.Engine == searchEngine) return option;
        }

        return AvailableSearchEngines.Count > 0 ? AvailableSearchEngines[0] : null;
    }

    private void RaiseThemeProperties()
    {
        OnPropertyChanged(nameof(IsDarkThemeSelected));
        OnPropertyChanged(nameof(IsBlackThemeSelected));
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    public void Dispose()
    {
        _themeService.ThemeChanged -= ThemeService_ThemeChanged;
        _autoStartService.EnabledChanged -= AutoStartService_EnabledChanged;
        _hoverActivationService.EnabledChanged -= HoverActivationService_EnabledChanged;
        _browserService.SettingsChanged -= BrowserService_SettingsChanged;
        _fileShelfService.StorageFolderChanged -= FileShelfService_StorageFolderChanged;
        _featureLayoutService.LayoutChanged -= FeatureLayoutService_LayoutChanged;
    }
}

public sealed class FeatureOptionViewModel
{
    public FeatureOptionViewModel(
        AppFeature feature,
        string displayName,
        bool isVisible,
        bool canToggleVisibility)
    {
        Feature = feature;
        DisplayName = displayName;
        IsVisible = isVisible;
        CanToggleVisibility = canToggleVisibility;
    }

    public AppFeature Feature { get; }

    public string DisplayName { get; }

    public bool IsVisible { get; }

    public bool CanToggleVisibility { get; }
}
