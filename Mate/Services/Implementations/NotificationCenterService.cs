using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mate.Models;
using Mate.Services.Interfaces;

namespace Mate.Services.Implementations;

public sealed class NotificationCenterService : INotificationCenterService
{
    private const int MaximumHistorySize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly ObservableCollection<MateNotification> _notifications = new();
    private readonly string _storagePath;

    public NotificationCenterService()
    {
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LD.Desktop.Mate");
        _storagePath = Path.Combine(appDataDirectory, "notifications.json");
        Notifications = new ReadOnlyObservableCollection<MateNotification>(_notifications);

        foreach (var item in Load().OrderByDescending(item => item.CreatedAt).Take(MaximumHistorySize))
        {
            _notifications.Add(item);
        }
    }

    public ReadOnlyObservableCollection<MateNotification> Notifications { get; }

    public event EventHandler<MateNotification>? NotificationReceived;

    public event EventHandler? HistoryChanged;

    public void Publish(
        string title,
        string message,
        MateNotificationKind kind = MateNotificationKind.Information,
        string? key = null,
        bool showBanner = true,
        bool isPersistent = false,
        string? actionId = null)
    {
        title = title.Trim();
        message = message.Trim();
        key = string.IsNullOrWhiteSpace(key) ? null : key.Trim();

        if (title.Length == 0 || message.Length == 0) return;
        if (key is not null && _notifications.Any(item => item.Key == key)) return;

        var notification = new MateNotification
        {
            Key = key,
            Title = title,
            Message = message,
            Kind = kind,
            IsPersistent = isPersistent,
            ActionId = string.IsNullOrWhiteSpace(actionId) ? null : actionId.Trim(),
            CreatedAt = DateTimeOffset.Now
        };

        _notifications.Insert(0, notification);
        while (_notifications.Count > MaximumHistorySize)
        {
            _notifications.RemoveAt(_notifications.Count - 1);
        }

        Save();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        if (showBanner) NotificationReceived?.Invoke(this, notification);
    }

    public void Clear()
    {
        if (_notifications.Count == 0) return;

        _notifications.Clear();
        Save();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(Guid notificationId)
    {
        var notification = _notifications.FirstOrDefault(item => item.Id == notificationId);
        if (notification is null) return;

        _notifications.Remove(notification);
        Save();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private IEnumerable<MateNotification> Load()
    {
        try
        {
            if (!File.Exists(_storagePath)) return Array.Empty<MateNotification>();

            var json = File.ReadAllText(_storagePath);
            return JsonSerializer.Deserialize<List<MateNotification>>(json, JsonOptions)
                   ?? new List<MateNotification>();
        }
        catch (IOException)
        {
            return Array.Empty<MateNotification>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<MateNotification>();
        }
        catch (JsonException)
        {
            return Array.Empty<MateNotification>();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_storagePath);
            if (directory is not null) Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_notifications, JsonOptions);
            File.WriteAllText(_storagePath, json);
        }
        catch (IOException)
        {
            // Notification history must never interrupt the application.
        }
        catch (UnauthorizedAccessException)
        {
            // Notification history must never interrupt the application.
        }
    }
}
