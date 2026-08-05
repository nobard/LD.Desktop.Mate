using System;

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
    bool CanSkipNext)
{
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
        false);
}
