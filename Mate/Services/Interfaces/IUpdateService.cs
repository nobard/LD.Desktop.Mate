using System;
using System.Threading;
using System.Threading.Tasks;
using Mate.Models;

namespace Mate.Services.Interfaces;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    Task<string> DownloadInstallerAsync(
        Uri installerDownloadUri,
        Version version,
        CancellationToken cancellationToken = default);
}
