using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mate.Models;
using Mate.Services.Interfaces;
using Velopack;
using Velopack.Sources;

namespace Mate.Services.Implementations;

public sealed class VelopackUpdateService : IUpdateService
{
    private const string RepositoryUrl =
        "https://github.com/nobard/LD.Desktop.Mate";

    private readonly UpdateManager _updateManager;
    private UpdateInfo? _availableUpdate;

    public VelopackUpdateService()
    {
        var source = new GithubSource(
            RepositoryUrl,
            accessToken: null,
            prerelease: IsPrereleaseBuild());
        _updateManager = new UpdateManager(source);
    }

    public string CurrentVersion => GetCurrentVersionText();

    public async Task<UpdateCheckResult> CheckForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        var currentVersion = CurrentVersion;
        if (!_updateManager.IsInstalled)
        {
            _availableUpdate = null;
            return new UpdateCheckResult(currentVersion, null, false);
        }

        var update = await _updateManager
            .CheckForUpdatesAsync()
            .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        _availableUpdate = update;

        return new UpdateCheckResult(
            currentVersion,
            update?.TargetFullRelease.Version.ToString(),
            true);
    }

    public async Task DownloadUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        var update = _availableUpdate
                     ?? throw new InvalidOperationException(
                         "No Velopack update is ready to download.");

        await _updateManager.DownloadUpdatesAsync(
            update,
            cancelToken: cancellationToken);
    }

    public void ApplyUpdateAndRestart()
    {
        var update = _availableUpdate
                     ?? throw new InvalidOperationException(
                         "No Velopack update is ready to apply.");

        _updateManager.WaitExitThenApplyUpdates(
            update.TargetFullRelease,
            silent: true,
            restart: true);
    }

    private string GetCurrentVersionText()
    {
        if (_updateManager.CurrentVersion is not null)
        {
            return _updateManager.CurrentVersion.ToString();
        }

        var informationalVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataIndex = informationalVersion.IndexOf('+');
            return metadataIndex >= 0
                ? informationalVersion[..metadataIndex]
                : informationalVersion;
        }

        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
               ?? "0.0.0";
    }

    private static bool IsPrereleaseBuild()
    {
        var informationalVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informationalVersion)) return false;

        var metadataIndex = informationalVersion.IndexOf('+');
        var versionWithoutMetadata = metadataIndex >= 0
            ? informationalVersion[..metadataIndex]
            : informationalVersion;
        return versionWithoutMetadata.Contains('-', StringComparison.Ordinal);
    }
}
