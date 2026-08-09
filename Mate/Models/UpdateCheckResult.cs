using System;

namespace Mate.Models;

public sealed record UpdateCheckResult(
    Version CurrentVersion,
    Version? LatestVersion,
    Uri? ReleasePageUri,
    Uri? InstallerDownloadUri)
{
    public bool HasPublishedRelease => LatestVersion is not null;

    public bool IsUpdateAvailable => LatestVersion is not null
                                     && LatestVersion > CurrentVersion
                                     && InstallerDownloadUri is not null;
}
