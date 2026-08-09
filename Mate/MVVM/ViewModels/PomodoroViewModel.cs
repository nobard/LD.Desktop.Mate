using System;
using System.IO;
using System.Windows.Threading;
using Mate.Models;
using Mate.MVVM.Core;
using Mate.Services.Interfaces;

namespace Mate.MVVM.ViewModels;

public sealed class PomodoroViewModel : ToolViewModel, IDisposable
{
    private readonly INotificationCenterService _notificationCenterService;
    private readonly IPomodoroSettingsService _settingsService;
    private readonly DispatcherTimer _timer;
    private TimeSpan _focusDuration = TimeSpan.FromMinutes(25);
    private TimeSpan _shortBreakDuration = TimeSpan.FromMinutes(5);
    private TimeSpan _longBreakDuration = TimeSpan.FromMinutes(15);
    private int _sessionsBeforeLongBreak = 4;
    private PomodoroPhase _phase = PomodoroPhase.Focus;
    private TimeSpan _remaining = TimeSpan.FromMinutes(25);
    private TimeSpan _phaseDuration = TimeSpan.FromMinutes(25);
    private DateTime _endsAtUtc;
    private bool _isRunning;
    private bool _isSettingsOpen;
    private int _completedFocusSessions;
    private string _editingFocusMinutes = "25";
    private string _editingShortBreakMinutes = "5";
    private string _editingLongBreakMinutes = "15";
    private string _editingSessionsBeforeLongBreak = "4";
    private string _settingsError = string.Empty;

    public PomodoroViewModel(
        INotificationCenterService notificationCenterService,
        IPomodoroSettingsService settingsService)
    {
        _notificationCenterService = notificationCenterService;
        _settingsService = settingsService;

        StartPauseCommand = new DelegateCommand(_ => ToggleTimer());
        ResetCommand = new DelegateCommand(_ => ResetCurrentPhase());
        SkipCommand = new DelegateCommand(_ => AdvancePhase(showNotification: false));
        OpenSettingsCommand = new DelegateCommand(_ => OpenSettings());
        SaveSettingsCommand = new DelegateCommand(_ => SaveSettings());
        ResetSettingsCommand = new DelegateCommand(_ => ResetEditingSettings());
        CancelSettingsCommand = new DelegateCommand(_ => IsSettingsOpen = false);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += Timer_Tick;
        ApplySettings(NormalizeSettings(_settingsService.Load()), resetCycle: true);
    }

    public override string Title => "Помодоро";

    public override string Description => "Таймер концентрации по технике Pomodoro.";

    public DelegateCommand StartPauseCommand { get; }

    public DelegateCommand ResetCommand { get; }

    public DelegateCommand SkipCommand { get; }

    public DelegateCommand OpenSettingsCommand { get; }

    public DelegateCommand SaveSettingsCommand { get; }

    public DelegateCommand ResetSettingsCommand { get; }

    public DelegateCommand CancelSettingsCommand { get; }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        private set => SetProperty(ref _isSettingsOpen, value);
    }

    public string EditingFocusMinutes
    {
        get => _editingFocusMinutes;
        set
        {
            if (SetProperty(ref _editingFocusMinutes, value)) SettingsError = string.Empty;
        }
    }

    public string EditingShortBreakMinutes
    {
        get => _editingShortBreakMinutes;
        set
        {
            if (SetProperty(ref _editingShortBreakMinutes, value)) SettingsError = string.Empty;
        }
    }

    public string EditingLongBreakMinutes
    {
        get => _editingLongBreakMinutes;
        set
        {
            if (SetProperty(ref _editingLongBreakMinutes, value)) SettingsError = string.Empty;
        }
    }

    public string EditingSessionsBeforeLongBreak
    {
        get => _editingSessionsBeforeLongBreak;
        set
        {
            if (SetProperty(ref _editingSessionsBeforeLongBreak, value)) SettingsError = string.Empty;
        }
    }

    public string SettingsError
    {
        get => _settingsError;
        private set => SetProperty(ref _settingsError, value);
    }

    public string PhaseTitle => _phase switch
    {
        PomodoroPhase.Focus => "ФОКУС",
        PomodoroPhase.ShortBreak => "КОРОТКИЙ ПЕРЕРЫВ",
        _ => "ДЛИННЫЙ ПЕРЕРЫВ"
    };

    public string PhaseDescription => _phase switch
    {
        PomodoroPhase.Focus => "Время сосредоточиться на одной задаче",
        PomodoroPhase.ShortBreak => "Небольшая пауза перед следующим подходом",
        _ => "Цикл завершён — можно отдохнуть подольше"
    };

    public string TimeText => $"{(int)_remaining.TotalMinutes:00}:{_remaining.Seconds:00}";

    public string CycleText =>
        $"Pomodoro {_completedFocusSessions % _sessionsBeforeLongBreak + 1} из {_sessionsBeforeLongBreak}";

    public string CompletedText => _completedFocusSessions == 0
        ? "Завершённых подходов пока нет"
        : $"Завершено подходов: {_completedFocusSessions}";

    public string PrimaryButtonText => IsRunning
        ? "Пауза"
        : _remaining < _phaseDuration
            ? "Продолжить"
            : "Начать";

    public double RemainingPercentage => _phaseDuration.TotalMilliseconds <= 0
        ? 0
        : Math.Clamp(
            _remaining.TotalMilliseconds / _phaseDuration.TotalMilliseconds * 100,
            0,
            100);

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetProperty(ref _isRunning, value)) return;
            OnPropertyChanged(nameof(PrimaryButtonText));
        }
    }

    private void ToggleTimer()
    {
        if (IsRunning)
        {
            PauseTimer();
            return;
        }

        _endsAtUtc = DateTime.UtcNow + _remaining;
        IsRunning = true;
        _timer.Start();
    }

    private void PauseTimer()
    {
        UpdateRemainingTime();
        _timer.Stop();
        IsRunning = false;
    }

    private void ResetCurrentPhase()
    {
        _timer.Stop();
        IsRunning = false;
        _remaining = _phaseDuration;
        NotifyTimerChanged();
    }

    private void OpenSettings()
    {
        if (IsRunning) PauseTimer();

        EditingFocusMinutes = ((int)_focusDuration.TotalMinutes).ToString();
        EditingShortBreakMinutes = ((int)_shortBreakDuration.TotalMinutes).ToString();
        EditingLongBreakMinutes = ((int)_longBreakDuration.TotalMinutes).ToString();
        EditingSessionsBeforeLongBreak = _sessionsBeforeLongBreak.ToString();
        SettingsError = string.Empty;
        IsSettingsOpen = true;
    }

    private void SaveSettings()
    {
        if (!TryReadSetting(EditingFocusMinutes, 1, 180, out var focusMinutes)
            || !TryReadSetting(EditingShortBreakMinutes, 1, 60, out var shortBreakMinutes)
            || !TryReadSetting(EditingLongBreakMinutes, 1, 120, out var longBreakMinutes)
            || !TryReadSetting(EditingSessionsBeforeLongBreak, 1, 12, out var sessionsBeforeLongBreak))
        {
            SettingsError = "Проверьте значения и допустимые диапазоны";
            return;
        }

        var settings = new PomodoroSettings(
            focusMinutes,
            shortBreakMinutes,
            longBreakMinutes,
            sessionsBeforeLongBreak);

        try
        {
            _settingsService.Save(settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SettingsError = "Не удалось сохранить настройки";
            return;
        }

        ApplySettings(settings, resetCycle: true);
        IsSettingsOpen = false;
    }

    private void ResetEditingSettings()
    {
        var defaults = PomodoroSettings.Default;
        EditingFocusMinutes = defaults.FocusMinutes.ToString();
        EditingShortBreakMinutes = defaults.ShortBreakMinutes.ToString();
        EditingLongBreakMinutes = defaults.LongBreakMinutes.ToString();
        EditingSessionsBeforeLongBreak = defaults.SessionsBeforeLongBreak.ToString();
        SettingsError = string.Empty;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        UpdateRemainingTime();
        if (_remaining > TimeSpan.Zero) return;

        _timer.Stop();
        IsRunning = false;
        AdvancePhase(showNotification: true);
    }

    private void UpdateRemainingTime()
    {
        var remaining = _endsAtUtc - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        var roundedSeconds = Math.Ceiling(remaining.TotalSeconds);
        var rounded = TimeSpan.FromSeconds(roundedSeconds);
        if (rounded == _remaining) return;

        _remaining = rounded;
        NotifyTimerChanged();
    }

    private void AdvancePhase(bool showNotification)
    {
        _timer.Stop();
        IsRunning = false;

        if (_phase == PomodoroPhase.Focus)
        {
            if (showNotification) _completedFocusSessions++;
            var isLongBreak = showNotification
                              && _completedFocusSessions % _sessionsBeforeLongBreak == 0;
            SetPhase(isLongBreak ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak);

            if (showNotification)
            {
                _notificationCenterService.Publish(
                    "Фокус завершён",
                    isLongBreak
                        ? "Цикл завершён. Время длинного перерыва."
                        : "Время сделать короткий перерыв.",
                    MateNotificationKind.Success,
                    isPersistent: true,
                    actionId: MateNotificationActions.OpenPomodoro);
            }

            return;
        }

        SetPhase(PomodoroPhase.Focus);
        if (showNotification)
        {
            _notificationCenterService.Publish(
                "Перерыв завершён",
                "Можно начинать следующий подход.",
                MateNotificationKind.Information,
                isPersistent: true,
                actionId: MateNotificationActions.OpenPomodoro);
        }
    }

    private void SetPhase(PomodoroPhase phase)
    {
        _phase = phase;
        _phaseDuration = phase switch
        {
            PomodoroPhase.Focus => _focusDuration,
            PomodoroPhase.ShortBreak => _shortBreakDuration,
            _ => _longBreakDuration
        };
        _remaining = _phaseDuration;

        OnPropertyChanged(nameof(PhaseTitle));
        OnPropertyChanged(nameof(PhaseDescription));
        OnPropertyChanged(nameof(CycleText));
        OnPropertyChanged(nameof(CompletedText));
        NotifyTimerChanged();
    }

    private void ApplySettings(PomodoroSettings settings, bool resetCycle)
    {
        _focusDuration = TimeSpan.FromMinutes(settings.FocusMinutes);
        _shortBreakDuration = TimeSpan.FromMinutes(settings.ShortBreakMinutes);
        _longBreakDuration = TimeSpan.FromMinutes(settings.LongBreakMinutes);
        _sessionsBeforeLongBreak = settings.SessionsBeforeLongBreak;

        _timer.Stop();
        IsRunning = false;
        if (resetCycle) _completedFocusSessions = 0;
        SetPhase(PomodoroPhase.Focus);
    }

    private static PomodoroSettings NormalizeSettings(PomodoroSettings settings) => new(
        Math.Clamp(settings.FocusMinutes, 1, 180),
        Math.Clamp(settings.ShortBreakMinutes, 1, 60),
        Math.Clamp(settings.LongBreakMinutes, 1, 120),
        Math.Clamp(settings.SessionsBeforeLongBreak, 1, 12));

    private static bool TryReadSetting(string value, int minimum, int maximum, out int result) =>
        int.TryParse(value, out result) && result >= minimum && result <= maximum;

    private void NotifyTimerChanged()
    {
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(RemainingPercentage));
        OnPropertyChanged(nameof(PrimaryButtonText));
        OnPropertyChanged(nameof(CycleText));
        OnPropertyChanged(nameof(CompletedText));
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
    }

    private enum PomodoroPhase
    {
        Focus,
        ShortBreak,
        LongBreak
    }
}
