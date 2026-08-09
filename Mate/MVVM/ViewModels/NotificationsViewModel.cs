using System;
using System.Collections.ObjectModel;
using Mate.Models;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class NotificationsViewModel : ToolViewModel
{
    private readonly INotificationCenterService _notificationCenterService;

    public NotificationsViewModel(INotificationCenterService notificationCenterService)
    {
        _notificationCenterService = notificationCenterService;
        Notifications = notificationCenterService.Notifications;
        ClearCommand = new DelegateCommand(
            _ => _notificationCenterService.Clear(),
            _ => HasNotifications);
        _notificationCenterService.HistoryChanged += NotificationCenterService_HistoryChanged;
    }

    public override string Title => "Уведомления";

    public override string Description => "История уведомлений Mate.";

    public ReadOnlyObservableCollection<MateNotification> Notifications { get; }

    public DelegateCommand ClearCommand { get; }

    public bool HasNotifications => Notifications.Count > 0;

    public bool IsEmpty => !HasNotifications;

    public string SummaryText => HasNotifications
        ? $"Уведомлений: {Notifications.Count}"
        : "Здесь появятся уведомления Mate";

    private void NotificationCenterService_HistoryChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SummaryText));
        ClearCommand.RaiseCanExecuteChanged();
    }
}
