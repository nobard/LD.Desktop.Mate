using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private readonly SemaphoreSlim _sessionChangeLock = new(1, 1);
    private readonly CancellationTokenSource _pollCancellation = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private Task? _pollTask;
    private string _title = string.Empty;
    private string _artist = string.Empty;
    private byte[]? _thumbnail;
    private bool _hasExplicitSourceSelection;
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
            _pollTask = PollSessionsAsync(_pollCancellation.Token);
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

    public async Task SeekAsync(TimeSpan position)
    {
        var session = _session;
        if (session is null) return;

        var timeline = session.GetTimelineProperties();
        var target = timeline.StartTime + position;
        if (target < timeline.StartTime) target = timeline.StartTime;
        if (timeline.EndTime > timeline.StartTime && target > timeline.EndTime) target = timeline.EndTime;

        await session.TryChangePlaybackPositionAsync(target.Ticks);
        await PublishSnapshotAsync(false);
    }

    public async Task SelectSourceAsync(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || _manager is null) return;

        await _sessionChangeLock.WaitAsync();
        try
        {
            var session = _manager.GetSessions().FirstOrDefault(candidate =>
                string.Equals(
                    candidate.SourceAppUserModelId,
                    sourceId,
                    StringComparison.OrdinalIgnoreCase));
            if (session is null) return;

            _hasExplicitSourceSelection = true;
            await SetActiveSessionAsync(session);
        }
        finally
        {
            _sessionChangeLock.Release();
        }
    }

    private async void Manager_CurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) => await ChangeCurrentSessionAsync();

    private async void Manager_SessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) => await ChangeCurrentSessionAsync();

    private async Task ChangeCurrentSessionAsync()
    {
        await _sessionChangeLock.WaitAsync();
        try
        {
            var manager = _manager;
            if (manager is null)
            {
                await SetActiveSessionAsync(null);
                return;
            }

            var sessions = manager.GetSessions();
            GlobalSystemMediaTransportControlsSession? nextSession = null;
            if (_hasExplicitSourceSelection && _session is not null)
            {
                nextSession = sessions.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.SourceAppUserModelId,
                        _session.SourceAppUserModelId,
                        StringComparison.OrdinalIgnoreCase));
                if (nextSession is null) _hasExplicitSourceSelection = false;
            }

            if (nextSession is null)
            {
                var systemSession = manager.GetCurrentSession();
                nextSession = systemSession is not null && IsPlaying(systemSession)
                    ? systemSession
                    : sessions.FirstOrDefault(IsPlaying) ?? systemSession ?? sessions.FirstOrDefault();
            }

            await SetActiveSessionAsync(nextSession);
        }
        finally
        {
            _sessionChangeLock.Release();
        }
    }

    private async Task PollSessionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                await ChangeCurrentSessionAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // Some browsers publish their system media session with a delay.
            }
        }
    }

    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.GetPlaybackInfo().PlaybackStatus ==
                   GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch
        {
            return false;
        }
    }

    private async Task SetActiveSessionAsync(GlobalSystemMediaTransportControlsSession? session)
    {
        if (ReferenceEquals(_session, session))
        {
            await PublishSnapshotAsync(false);
            return;
        }

        DetachSessionEvents();
        _session = session;
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
                SessionChanged?.Invoke(MediaSessionSnapshot.Empty with
                {
                    Sources = GetAvailableSources()
                });
                return;
            }

            if (refreshMediaProperties)
            {
                try
                {
                    var media = await session.TryGetMediaPropertiesAsync();
                    _title = string.IsNullOrWhiteSpace(media.Title) ? "Без названия" : media.Title;
                    _artist = FirstNotEmpty(media.Artist, media.AlbumArtist, media.Subtitle);
                    _thumbnail = await ReadThumbnailAsync(media.Thumbnail);
                }
                catch
                {
                    _title = string.IsNullOrWhiteSpace(_title)
                        ? GetFriendlySourceName(session.SourceAppUserModelId)
                        : _title;
                    _artist = string.Empty;
                    _thumbnail = null;
                }
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
                controls.IsNextEnabled,
                controls.IsPlaybackPositionEnabled)
            {
                Sources = GetAvailableSources(),
                SelectedSourceId = session.SourceAppUserModelId
            });
        }
        catch
        {
            SessionChanged?.Invoke(MediaSessionSnapshot.Empty with
            {
                Sources = GetAvailableSources(),
                SelectedSourceId = _session?.SourceAppUserModelId
            });
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null) return null;

        try
        {
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
        catch
        {
            return null;
        }
    }

    private static string FirstNotEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return string.Empty;
    }

    private IReadOnlyList<MediaSourceSnapshot> GetAvailableSources()
    {
        var sessions = _manager?.GetSessions();
        if (sessions is null || sessions.Count == 0) return Array.Empty<MediaSourceSnapshot>();

        var sources = new List<MediaSourceSnapshot>(sessions.Count);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in sessions)
        {
            var id = session.SourceAppUserModelId;
            if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id)) continue;
            sources.Add(new MediaSourceSnapshot(id, GetFriendlySourceName(id)));
        }

        return sources;
    }

    private static string GetFriendlySourceName(string sourceAppId)
    {
        if (sourceAppId.Contains("yandex", StringComparison.OrdinalIgnoreCase)) return "Yandex Browser";
        if (sourceAppId.Contains("chrome", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
        if (sourceAppId.Contains("msedge", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
        if (sourceAppId.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        if (sourceAppId.Contains("firefox", StringComparison.OrdinalIgnoreCase)) return "Mozilla Firefox";

        var fileName = Path.GetFileNameWithoutExtension(sourceAppId);
        if (string.Equals(fileName, "browser", StringComparison.OrdinalIgnoreCase)) return "Yandex Browser";
        return string.IsNullOrWhiteSpace(fileName) ? sourceAppId : fileName;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pollCancellation.Cancel();

        DetachSessionEvents();
        if (_manager is not null)
        {
            _manager.CurrentSessionChanged -= Manager_CurrentSessionChanged;
            _manager.SessionsChanged -= Manager_SessionsChanged;
        }

        _pollTask = null;
    }
}
