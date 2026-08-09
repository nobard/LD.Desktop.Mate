using System.Threading;
using System.Threading.Tasks;
using Mate.Models;

namespace Mate.Services.Interfaces;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    Task DownloadUpdateAsync(CancellationToken cancellationToken = default);

    void ApplyUpdateAndRestart();
}
