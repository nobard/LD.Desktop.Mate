using System;
using System.Collections.Generic;

namespace Mate.Models;

public sealed record MediaSessionSnapshot(
    bool IsAvailable,
    string Title,
    string Artist,
    string Source,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    byte[]? Thumbnail,
    bool CanTogglePlayPause,
    bool CanSkipPrevious,
    bool CanSkipNext,
    bool CanSeek)
{
    public IReadOnlyList<MediaSourceSnapshot> Sources { get; init; } = Array.Empty<MediaSourceSnapshot>();

    public string? SelectedSourceId { get; init; }

    public static MediaSessionSnapshot Empty { get; } = new(
        false,
        "Нет активного медиа",
        "Откройте видео или музыку",
        string.Empty,
        false,
        TimeSpan.Zero,
        TimeSpan.Zero,
        null,
        false,
        false,
        false,
        false);
}
