using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using Mate.Models;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class ClipboardViewModel : ToolViewModel, IDisposable
{
    private readonly IClipboardHistoryService _clipboardHistoryService;
    private bool _isLoading;
    private bool _refreshAgain;
    private string _statusMessage = "Загрузка истории буфера…";
    private string _emptyMessage = "История буфера пуста";
    private bool _disposed;

    public ClipboardViewModel(IClipboardHistoryService clipboardHistoryService)
    {
        _clipboardHistoryService = clipboardHistoryService;
        Items = new ObservableCollection<ClipboardCardViewModel>();

        CopyItemCommand = new DelegateCommand(CopyItem);
        TogglePinCommand = new DelegateCommand(TogglePin);
        ClearHistoryCommand = new DelegateCommand(_ => ClearHistory());
        RefreshCommand = new DelegateCommand(_ => _ = RefreshAsync(), _ => !IsLoading);

        _clipboardHistoryService.HistoryChanged += ClipboardHistoryService_HistoryChanged;
        _ = RefreshAsync();
    }

    public override string Title => "Буфер обмена";

    public override string Description => "История системного буфера Windows.";

    public ObservableCollection<ClipboardCardViewModel> Items { get; }

    public DelegateCommand CopyItemCommand { get; }

    public DelegateCommand TogglePinCommand { get; }

    public DelegateCommand ClearHistoryCommand { get; }

    public DelegateCommand RefreshCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value)) return;
            OnPropertyChanged(nameof(IsEmpty));
            RefreshCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsEmpty => !IsLoading && Items.Count == 0;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string EmptyMessage
    {
        get => _emptyMessage;
        private set => SetProperty(ref _emptyMessage, value);
    }

    private async Task RefreshAsync()
    {
        if (_disposed) return;
        if (IsLoading)
        {
            _refreshAgain = true;
            return;
        }

        do
        {
            _refreshAgain = false;
            IsLoading = true;
            StatusMessage = "Загрузка истории буфера…";

            var snapshot = await _clipboardHistoryService.GetHistoryAsync();
            if (_disposed) return;

            Items.Clear();
            foreach (var item in snapshot.Items)
            {
                Items.Add(new ClipboardCardViewModel(item));
            }

            ApplyState(snapshot.State);
            OnPropertyChanged(nameof(IsEmpty));
            IsLoading = false;
        }
        while (_refreshAgain && !_disposed);
    }

    private void ApplyState(ClipboardHistoryState state)
    {
        switch (state)
        {
            case ClipboardHistoryState.Available:
                StatusMessage = Items.Count == 0
                    ? "История буфера пуста"
                    : $"Текстовых элементов: {Items.Count} · нажмите на элемент, чтобы скопировать";
                EmptyMessage = "Скопированный текст появится здесь";
                break;
            case ClipboardHistoryState.Disabled:
                StatusMessage = "История буфера Windows выключена";
                EmptyMessage = "Нажмите Win + V и включите историю буфера";
                break;
            case ClipboardHistoryState.AccessDenied:
                StatusMessage = "Windows запретила доступ к истории буфера";
                EmptyMessage = "Проверьте настройки буфера обмена Windows";
                break;
            default:
                StatusMessage = "Не удалось прочитать историю буфера";
                EmptyMessage = "Попробуйте обновить страницу";
                break;
        }
    }

    private async void CopyItem(object? parameter)
    {
        if (parameter is not ClipboardCardViewModel item) return;

        foreach (var card in Items) card.IsCopied = false;
        if (!_clipboardHistoryService.SetCurrent(item.Snapshot))
        {
            StatusMessage = "Не удалось скопировать элемент";
            return;
        }

        item.IsCopied = true;
        await Task.Delay(1200);
        if (!_disposed) item.IsCopied = false;
    }

    private void TogglePin(object? parameter)
    {
        if (parameter is ClipboardCardViewModel item)
        {
            if (!_clipboardHistoryService.TogglePinned(item.Snapshot))
            {
                StatusMessage = "Не удалось вернуть элемент в буфер обмена";
            }
        }
    }

    private void ClearHistory()
    {
        if (!_clipboardHistoryService.ClearHistory())
        {
            StatusMessage = "Не удалось очистить историю буфера";
        }
    }

    private void ClipboardHistoryService_HistoryChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            _ = RefreshAsync();
            return;
        }

        dispatcher.BeginInvoke(new Action(() => _ = RefreshAsync()));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _clipboardHistoryService.HistoryChanged -= ClipboardHistoryService_HistoryChanged;
    }
}

public sealed class ClipboardCardViewModel : ObservableObject
{
    private bool _isCopied;

    public ClipboardCardViewModel(ClipboardItemSnapshot snapshot)
    {
        Snapshot = snapshot;
        Preview = string.IsNullOrWhiteSpace(snapshot.Preview) ? "Пустой элемент" : snapshot.Preview.Trim();
        IsPinned = snapshot.IsPinned;
    }

    public ClipboardItemSnapshot Snapshot { get; }

    public string Preview { get; }

    public bool IsPinned { get; }

    public bool IsCopied
    {
        get => _isCopied;
        set => SetProperty(ref _isCopied, value);
    }

}
