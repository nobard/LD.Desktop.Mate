using System;
using System.ComponentModel;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class IncognitoViewModel : ToolViewModel, IDisposable
{
    private readonly IPrivateBrowserService _privateBrowserService;
    private string _searchQuery = string.Empty;
    private string _errorMessage = string.Empty;

    public IncognitoViewModel(IPrivateBrowserService privateBrowserService)
    {
        _privateBrowserService = privateBrowserService;
        _privateBrowserService.SettingsChanged += BrowserService_SettingsChanged;
        SearchCommand = new DelegateCommand(_ => Search(), _ => !string.IsNullOrWhiteSpace(SearchQuery));
        ClearSearchCommand = new DelegateCommand(_ => SearchQuery = string.Empty, _ => HasSearchQuery);
    }

    public override string Title => "Браузер";

    public override string Description => "Поиск в выбранном браузере.";

    public string ModeDescription => _privateBrowserService.Settings.UsePrivateMode
        ? "Откроется новое приватное окно выбранного браузера"
        : "Откроется новое окно выбранного браузера";

    public DelegateCommand SearchCommand { get; }

    public DelegateCommand ClearSearchCommand { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (!SetProperty(ref _searchQuery, value)) return;
            ErrorMessage = string.Empty;
            SearchCommand.RaiseCanExecuteChanged();
            ClearSearchCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasSearchQuery));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool HasSearchQuery => !string.IsNullOrEmpty(SearchQuery);

    private void Search()
    {
        try
        {
            ErrorMessage = _privateBrowserService.OpenSearch(SearchQuery) switch
            {
                PrivateBrowserOpenResult.Opened => string.Empty,
                PrivateBrowserOpenResult.UnsupportedBrowser => "Ваш браузер не поддерживается",
                _ => "Поддерживаемый браузер не найден"
            };
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            ErrorMessage = "Не удалось открыть приватное окно браузера";
        }
    }

    private void BrowserService_SettingsChanged(object? sender, EventArgs e) =>
        OnPropertyChanged(nameof(ModeDescription));

    public void Dispose() =>
        _privateBrowserService.SettingsChanged -= BrowserService_SettingsChanged;
}
