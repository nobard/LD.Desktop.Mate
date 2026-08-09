using System;
using System.Collections.ObjectModel;
using Mate.Models;

namespace Mate.Services.Interfaces;

public interface INotificationCenterService
{
    ReadOnlyObservableCollection<MateNotification> Notifications { get; }

    event EventHandler<MateNotification>? NotificationReceived;

    event EventHandler? HistoryChanged;

    void Publish(
        string title,
        string message,
        MateNotificationKind kind = MateNotificationKind.Information,
        string? key = null,
        bool showBanner = true,
        bool isPersistent = false,
        string? actionId = null);

    void Clear();

    void Remove(Guid notificationId);
}
