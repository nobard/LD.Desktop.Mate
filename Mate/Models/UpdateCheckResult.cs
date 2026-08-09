namespace Mate.Models;

public sealed record UpdateCheckResult(
    string CurrentVersion,
    string? LatestVersion,
    bool CanCheckForUpdates)
{
    public bool HasPublishedRelease => CanCheckForUpdates;

    public bool IsUpdateAvailable => LatestVersion is not null;
}
