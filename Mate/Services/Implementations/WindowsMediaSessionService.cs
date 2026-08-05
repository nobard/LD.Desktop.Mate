using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Mate.Models;
using Mate.Services.Interfaces;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Mate.Services.Implementations;

public sealed class WindowsMediaSessionService : IMediaSessionService
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string _title = string.Empty;
    private string _artist = string.Empty;
    private byte[]? _thumbnail;
    private bool _disposed;

    public event Action<MediaSessionSnapshot>? SessionChanged;

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
            _manager.SessionsChanged += Manager_SessionsChanged;
            await ChangeCurrentSessionAsync();
        }
        catch
        {
            SessionChanged?.Invoke(MediaSessionSnapshot.Empty);
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        var session = _session;
        if (session is null) return;

        await session.TryTogglePlayPauseAsync();
        await PublishSnapshotAsync(false);
    }

    public async Task SkipPreviousAsync()
    {
        var session = _session;
        if (session is null) return;

        await session.TrySkipPreviousAsync();
    }

    public async Task SkipNextAsync()
    {
        var session = _session;
        if (session is null) return;

        await session.TrySkipNextAsync();
    }

    private async void Manager_CurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) => await ChangeCurrentSessionAsync();

    private async void Manager_SessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) => await ChangeCurrentSessionAsync();

    private async Task ChangeCurrentSessionAsync()
    {
        DetachSessionEvents();
        _session = _manager?.GetCurrentSession();
        _title = string.Empty;
        _artist = string.Empty;
        _thumbnail = null;
        AttachSessionEvents();
        await PublishSnapshotAsync(true);
    }

    private void AttachSessionEvents()
    {
        if (_session is null) return;

        _session.MediaPropertiesChanged += Session_MediaPropertiesChanged;
        _session.PlaybackInfoChanged += Session_PlaybackInfoChanged;
        _session.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
    }

    private void DetachSessionEvents()
    {
        if (_session is null) return;

        _session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
        _session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
        _session.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
    }

    private async void Session_MediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args) => await PublishSnapshotAsync(true);

    private async void Session_PlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) => await PublishSnapshotAsync(false);

    private async void Session_TimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args) => await PublishSnapshotAsync(false);

    private async Task PublishSnapshotAsync(bool refreshMediaProperties)
    {
        await _refreshLock.WaitAsync();
        try
        {
            var session = _session;
            if (session is null)
            {
                SessionChanged?.Invoke(MediaSessionSnapshot.Empty);
                return;
            }

            if (refreshMediaProperties)
            {
                var media = await session.TryGetMediaPropertiesAsync();
                _title = string.IsNullOrWhiteSpace(media.Title) ? "Без названия" : media.Title;
                _artist = FirstNotEmpty(media.Artist, media.AlbumArtist, media.Subtitle);
                _thumbnail = await ReadThumbnailAsync(media.Thumbnail);
            }

            if (!ReferenceEquals(session, _session)) return;

            var playback = session.GetPlaybackInfo();
            var timeline = session.GetTimelineProperties();
            var controls = playback.Controls;
            var duration = timeline.EndTime > timeline.StartTime
                ? timeline.EndTime - timeline.StartTime
                : TimeSpan.Zero;
            var position = timeline.Position > timeline.StartTime
                ? timeline.Position - timeline.StartTime
                : TimeSpan.Zero;
            if (duration > TimeSpan.Zero && position > duration)
            {
                position = duration;
            }

            SessionChanged?.Invoke(new MediaSessionSnapshot(
                true,
                _title,
                _artist,
                GetFriendlySourceName(session.SourceAppUserModelId),
                playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                position,
                duration,
                _thumbnail,
                controls.IsPlayEnabled || controls.IsPauseEnabled,
                controls.IsPreviousEnabled,
                controls.IsNextEnabled));
        }
        catch
        {
            SessionChanged?.Invoke(MediaSessionSnapshot.Empty);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null) return null;

        using var stream = await thumbnail.OpenReadAsync();
        if (stream.Size == 0 || stream.Size > 10 * 1024 * 1024) return null;

        var size = (uint)stream.Size;
        using var input = stream.GetInputStreamAt(0);
        using var reader = new DataReader(input);
        await reader.LoadAsync(size);
        var bytes = new byte[(int)size];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private static string FirstNotEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return string.Empty;
    }

    private static string GetFriendlySourceName(string sourceAppId)
    {
        if (sourceAppId.Contains("chrome", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
        if (sourceAppId.Contains("msedge", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
        if (sourceAppId.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        if (sourceAppId.Contains("firefox", StringComparison.OrdinalIgnoreCase)) return "Mozilla Firefox";

        var fileName = Path.GetFileNameWithoutExtension(sourceAppId);
        return string.IsNullOrWhiteSpace(fileName) ? sourceAppId : fileName;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DetachSessionEvents();
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
            _manager.SessionsChanged -= Manager_SessionsChanged;
        }

        _refreshLock.Dispose();
    }
}
