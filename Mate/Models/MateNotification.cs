using System;
using System.Text.Json.Serialization;

namespace Mate.Models;

public enum MateNotificationKind
{
    Information,
    Success,
    Warning,
    Error,
    Update
}

public sealed class MateNotification
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string? Key { get; init; }

    public string Source { get; init; } = "Mate";

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public MateNotificationKind Kind { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    [JsonIgnore]
    public string TimeText => CreatedAt.LocalDateTime.Date == DateTime.Today
        ? CreatedAt.LocalDateTime.ToString("HH:mm")
        : CreatedAt.LocalDateTime.ToString("dd.MM · HH:mm");
}
