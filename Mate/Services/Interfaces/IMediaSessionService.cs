using System;
using System.Threading.Tasks;
using Mate.Models;

namespace Mate.Services.Interfaces;

public interface IMediaSessionService : IDisposable
{
    event Action<MediaSessionSnapshot>? SessionChanged;

    Task InitializeAsync();

    Task TogglePlayPauseAsync();

    Task SkipPreviousAsync();

    Task SkipNextAsync();

    Task SeekAsync(TimeSpan position);

    Task SelectSourceAsync(string sourceId);
}
