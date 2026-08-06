using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mate.Models;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class MusicViewModel : ToolViewModel, IDisposable
{
    private readonly IMediaSessionService _mediaSessionService;
    private readonly DispatcherTimer _positionTimer;
    private readonly DispatcherTimer _seekTimer;
    private string _trackTitle = "Нет активного медиа";
    private string _artist = "Откройте видео или музыку";
    private string _source = string.Empty;
    private TimeSpan _position;
    private TimeSpan _duration;
    private ImageSource? _thumbnail;
    private bool _isPlaying;
    private bool _canTogglePlayPause;
    private bool _canSkipPrevious;
    private bool _canSkipNext;
    private bool _canSeek;
    private bool _isSeekPending;
    private double _pendingSeekPercentage;

    public MusicViewModel(IMediaSessionService mediaSessionService)
    {
        _mediaSessionService = mediaSessionService;
        _mediaSessionService.SessionChanged += MediaSessionService_SessionChanged;

        TogglePlayPauseCommand = new DelegateCommand(
            _ => _ = ExecuteSafelyAsync(_mediaSessionService.TogglePlayPauseAsync),
            _ => _canTogglePlayPause);
        SkipPreviousCommand = new DelegateCommand(
            _ => _ = ExecuteSafelyAsync(_mediaSessionService.SkipPreviousAsync),
            _ => _canSkipPrevious);
        SkipNextCommand = new DelegateCommand(
            _ => _ = ExecuteSafelyAsync(_mediaSessionService.SkipNextAsync),
            _ => _canSkipNext);

        _positionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _positionTimer.Tick += PositionTimer_Tick;
        _positionTimer.Start();

        _seekTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(140)
        };
        _seekTimer.Tick += SeekTimer_Tick;

        _ = ExecuteSafelyAsync(_mediaSessionService.InitializeAsync);
    }

    public override string Title => "Плеер";

    public override string Description => "Текущий системный медиасеанс Windows.";

    public override string HeaderInfo => Source;

    public string TrackTitle
    {
        get => _trackTitle;
        private set => SetProperty(ref _trackTitle, value);
    }

    public string Artist
    {
        get => _artist;
        private set => SetProperty(ref _artist, value);
    }

    public string Source
    {
        get => _source;
        private set
        {
            if (!SetProperty(ref _source, value)) return;
            OnPropertyChanged(nameof(HeaderInfo));
        }
    }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        private set => SetProperty(ref _thumbnail, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (!SetProperty(ref _isPlaying, value)) return;
            OnPropertyChanged(nameof(PlayPauseGlyph));
            OnPropertyChanged(nameof(PlayPauseToolTip));
        }
    }

    public string PlayPauseGlyph => IsPlaying ? "Ⅱ" : "▷";

    public string PlayPauseToolTip => IsPlaying ? "Пауза" : "Воспроизвести";

    public string ElapsedTime => FormatTime(_position);

    public string DurationTime => FormatTime(_duration);

    public double ProgressPercentage => _duration.TotalMilliseconds <= 0
        ? 0
        : Math.Clamp(_position.TotalMilliseconds / _duration.TotalMilliseconds * 100, 0, 100);

    public double SeekPercentage
    {
        get => ProgressPercentage;
        set
        {
            if (!CanSeek || _duration <= TimeSpan.Zero) return;

            _pendingSeekPercentage = Math.Clamp(value, 0, 100);
            _position = TimeSpan.FromMilliseconds(
                _duration.TotalMilliseconds * _pendingSeekPercentage / 100);
            _isSeekPending = true;
            NotifyTimelineChanged();
            _seekTimer.Stop();
            _seekTimer.Start();
        }
    }

    public bool CanSeek
    {
        get => _canSeek;
        private set => SetProperty(ref _canSeek, value);
    }

    public DelegateCommand TogglePlayPauseCommand { get; }

    public DelegateCommand SkipPreviousCommand { get; }

    public DelegateCommand SkipNextCommand { get; }

    private void MediaSessionService_SessionChanged(MediaSessionSnapshot snapshot)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplySnapshot(snapshot);
        }
        else
        {
            dispatcher.Invoke(() => ApplySnapshot(snapshot));
        }
    }

    private void ApplySnapshot(MediaSessionSnapshot snapshot)
    {
        TrackTitle = snapshot.Title;
        Artist = snapshot.Artist;
        Source = snapshot.Source;
        IsPlaying = snapshot.IsPlaying;
        _duration = snapshot.Duration;
        if (!_isSeekPending) _position = snapshot.Position;
        Thumbnail = CreateImage(snapshot.Thumbnail);

        _canTogglePlayPause = snapshot.CanTogglePlayPause;
        _canSkipPrevious = snapshot.CanSkipPrevious;
        _canSkipNext = snapshot.CanSkipNext;
        CanSeek = snapshot.CanSeek && snapshot.Duration > TimeSpan.Zero;
        TogglePlayPauseCommand.RaiseCanExecuteChanged();
        SkipPreviousCommand.RaiseCanExecuteChanged();
        SkipNextCommand.RaiseCanExecuteChanged();
        NotifyTimelineChanged();
    }

    private async void SeekTimer_Tick(object? sender, EventArgs e)
    {
        _seekTimer.Stop();
        if (!CanSeek || _duration <= TimeSpan.Zero)
        {
            _isSeekPending = false;
            return;
        }

        var target = TimeSpan.FromMilliseconds(
            _duration.TotalMilliseconds * _pendingSeekPercentage / 100);
        try
        {
            await _mediaSessionService.SeekAsync(target);
        }
        catch
        {
            // Some media sessions expose a timeline but reject seeking.
        }
        finally
        {
            _isSeekPending = false;
        }
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsPlaying || _duration <= TimeSpan.Zero) return;

        _position = _position.Add(TimeSpan.FromSeconds(1));
        if (_position > _duration) _position = _duration;
        NotifyTimelineChanged();
    }

    private void NotifyTimelineChanged()
    {
        OnPropertyChanged(nameof(ElapsedTime));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(SeekPercentage));
        OnPropertyChanged(nameof(DurationTime));
    }

    private static ImageSource? CreateImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;

        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"m\:ss");
    }

    private static async Task ExecuteSafelyAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch
        {
            // Some applications expose metadata but reject remote commands.
        }
    }

    public void Dispose()
    {
        _positionTimer.Stop();
        _positionTimer.Tick -= PositionTimer_Tick;
        _seekTimer.Stop();
        _seekTimer.Tick -= SeekTimer_Tick;
        _mediaSessionService.SessionChanged -= MediaSessionService_SessionChanged;
    }
}
