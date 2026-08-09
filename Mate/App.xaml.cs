using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Autofac;
using Mate.Models;
using Mate.MVVM.ViewModels;
using Mate.MVVM.Views;
using Mate.Services.DI;
using Mate.Services.Interfaces;
using Velopack;
using Forms = System.Windows.Forms;

namespace Mate;

public partial class App : Application
{
    private IContainer? _container;
    private ITrayService? _trayService;
    private IUpdateService? _updateService;
    private INotificationCenterService? _notificationCenterService;
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _mainWindowViewModel;
    private HotZoneWindow? _hotZoneWindow;
    private NotificationPopupWindow? _notificationPopupWindow;
    private DispatcherTimer? _pointerTimer;
    private DispatcherTimer? _updateTimer;
    private CancellationTokenSource? _updateCancellation;
    private DateTime _panelShownAtUtc;
    private DateTime? _pointerLeftAtUtc;
    private bool _pointerEnteredPanel;
    private bool _isCheckingForUpdates;
    private bool _isInstallingUpdate;

    private static readonly TimeSpan InitialPointerGracePeriod = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(220);

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build()
            .SetArgs(args)
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _container = AutofacConfig.GetConfiguredContainer();
        _mainWindow = _container.Resolve<MainWindow>();
        _mainWindowViewModel = _mainWindow.DataContext as MainWindowViewModel;
        _mainWindow.ApplyScreenScale();
        MainWindow = _mainWindow;

        _hotZoneWindow = new HotZoneWindow();
        _hotZoneWindow.ApplyScale(_mainWindow.InterfaceScale);
        _hotZoneWindow.PositionAtTopCenter();
        _hotZoneWindow.Show();

        _notificationCenterService = _container.Resolve<INotificationCenterService>();
        _notificationPopupWindow = new NotificationPopupWindow();
        _notificationPopupWindow.ApplyScale(_mainWindow.InterfaceScale);
        _notificationCenterService.NotificationReceived += NotificationCenterService_NotificationReceived;

        _trayService = _container.Resolve<ITrayService>();
        _updateService = _container.Resolve<IUpdateService>();
        _updateCancellation = new CancellationTokenSource();
        _trayService.Initialize(
            () => Dispatcher.Invoke(TogglePanel),
            () => Dispatcher.Invoke(() => _ = CheckForUpdatesAsync(showResult: true)),
            () => Dispatcher.Invoke(ExitApplication));

        _updateTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromHours(6)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
        _ = CheckForUpdatesAsync(showResult: false);

        _pointerTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(70)
        };
        _pointerTimer.Tick += PointerTimer_Tick;
        _pointerTimer.Start();
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e) =>
        _ = CheckForUpdatesAsync(showResult: false);

    private async Task CheckForUpdatesAsync(bool showResult)
    {
        if (_isCheckingForUpdates || _updateService is null || _updateCancellation is null) return;

        var cancellationToken = _updateCancellation.Token;
        _isCheckingForUpdates = true;
        if (showResult) _trayService?.SetUpdateCheckInProgress(true);

        try
        {
            var result = await _updateService.CheckForUpdateAsync(cancellationToken);
            if (result.IsUpdateAvailable && result.LatestVersion is not null)
            {
                var versionText = result.LatestVersion;
                Action installUpdate = () =>
                    _ = DownloadAndInstallUpdateAsync(versionText);
                _mainWindowViewModel?.ShowUpdateAvailable(versionText, installUpdate);
                _trayService?.ShowUpdateAvailable(versionText, installUpdate);
                _notificationCenterService?.Publish(
                    "Доступно обновление",
                    $"Версия {versionText} готова к установке.",
                    MateNotificationKind.Update,
                    key: $"update:{versionText}");
                return;
            }

            _mainWindowViewModel?.ClearUpdate();
            if (!showResult) return;
            var message = result.HasPublishedRelease
                ? $"Последняя версия: {result.CurrentVersion}"
                : "Проверка обновлений доступна после установки";
            _trayService?.ShowUpdateCheckMessage(message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Application is shutting down.
        }
        catch
        {
            if (showResult)
            {
                _trayService?.ShowUpdateCheckMessage("Ошибка проверки обновлений");
                _notificationCenterService?.Publish(
                    "Ошибка проверки обновлений",
                    "Не удалось связаться с сервером. Попробуйте ещё раз позже.",
                    MateNotificationKind.Error);
            }
        }
        finally
        {
            _isCheckingForUpdates = false;
            if (showResult) _trayService?.SetUpdateCheckInProgress(false);
        }
    }

    private async Task DownloadAndInstallUpdateAsync(string versionText)
    {
        if (_isInstallingUpdate || _updateService is null || _updateCancellation is null) return;

        var cancellationToken = _updateCancellation.Token;
        _isInstallingUpdate = true;
        _mainWindowViewModel?.SetUpdateInstallationInProgress();
        _trayService?.SetUpdateInstallationInProgress();

        try
        {
            await _updateService.DownloadUpdateAsync(cancellationToken);
            _updateService.ApplyUpdateAndRestart();
            ExitApplication();
            return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            Action retryUpdate = () =>
                _ = DownloadAndInstallUpdateAsync(versionText);
            _mainWindowViewModel?.ShowUpdateAvailable(versionText, retryUpdate);
            _trayService?.ShowUpdateAvailable(versionText, retryUpdate);
            _notificationCenterService?.Publish(
                "Не удалось обновить Mate",
                $"Версия {versionText} не установлена. Повторите попытку.",
                MateNotificationKind.Error);
        }
        finally
        {
            _isInstallingUpdate = false;
        }
    }

    private void TogglePanel()
    {
        if (_mainWindow is null) return;
        if (_mainWindow.IsVisible)
        {
            if (_mainWindow.IsClosingAnimation)
            {
                _mainWindow.CancelCloseAnimation(force: true);
            }
            else
            {
                HidePanel(cancelWhenPointerReturns: false);
            }
            return;
        }

        ShowPanel(activate: true);
    }

    private void ShowPanel(bool activate)
    {
        if (_mainWindow is null || _mainWindow.IsVisible) return;

        _mainWindow.PositionAtTopCenter();
        _mainWindow.PrepareOpenAnimation();
        _mainWindow.Show();
        _mainWindow.PlayOpenAnimation();
        if (activate) _mainWindow.Activate();

        _panelShownAtUtc = DateTime.UtcNow;
        _pointerLeftAtUtc = null;
        _pointerEnteredPanel = false;
    }

    private void HidePanel(bool cancelWhenPointerReturns = true)
    {
        if (_mainWindow is null || !_mainWindow.IsVisible) return;
        _mainWindow.HideAnimated(cancelWhenPointerReturns);
        _pointerLeftAtUtc = null;
        _pointerEnteredPanel = false;
    }

    private void PointerTimer_Tick(object? sender, EventArgs e)
    {
        if (_mainWindow is null) return;

        var pointer = Forms.Cursor.Position;
        var pointerInHotZone = IsPointerInTopCenterHotZone(pointer);

        if (!_mainWindow.IsVisible)
        {
            if (pointerInHotZone) ShowPanel(activate: false);
            return;
        }

        if (IsPointerInsidePanel(pointer))
        {
            _mainWindow.CancelCloseAnimation();
            _pointerEnteredPanel = true;
            _pointerLeftAtUtc = null;
            return;
        }

        if (pointerInHotZone)
        {
            _mainWindow.CancelCloseAnimation();
            _pointerLeftAtUtc = null;
            return;
        }

        if (_mainWindow.IsClosingAnimation) return;

        if (Forms.Control.MouseButtons != Forms.MouseButtons.None)
        {
            _pointerLeftAtUtc = null;
            return;
        }

        var now = DateTime.UtcNow;
        if (!_pointerEnteredPanel && now - _panelShownAtUtc < InitialPointerGracePeriod) return;

        _pointerLeftAtUtc ??= now;
        if (now - _pointerLeftAtUtc >= HideDelay) HidePanel();
    }

    private void NotificationCenterService_NotificationReceived(
        object? sender,
        MateNotification notification)
    {
        if (_notificationPopupWindow is null) return;

        if (Dispatcher.CheckAccess())
        {
            _notificationPopupWindow.ShowNotification(notification);
            return;
        }

        Dispatcher.BeginInvoke(
            new Action(() => _notificationPopupWindow?.ShowNotification(notification)));
    }

    private bool IsPointerInsidePanel(System.Drawing.Point pointer)
    {
        if (_mainWindow is null || !_mainWindow.IsVisible) return false;

        var topLeft = _mainWindow.PointToScreen(new Point(0, 0));
        var bottomRight = _mainWindow.PointToScreen(
            new Point(_mainWindow.ActualWidth, _mainWindow.ActualHeight));

        return pointer.X >= topLeft.X
               && pointer.X <= bottomRight.X
               && pointer.Y >= topLeft.Y
               && pointer.Y <= bottomRight.Y;
    }

    private bool IsPointerInTopCenterHotZone(System.Drawing.Point pointer)
    {
        if (_hotZoneWindow is { IsVisible: true })
        {
            var topLeft = _hotZoneWindow.PointToScreen(new Point(0, 0));
            var bottomRight = _hotZoneWindow.PointToScreen(
                new Point(_hotZoneWindow.ActualWidth, _hotZoneWindow.ActualHeight));

            return pointer.X >= topLeft.X
                   && pointer.X <= bottomRight.X
                   && pointer.Y >= topLeft.Y
                   && pointer.Y <= bottomRight.Y;
        }

        var screen = Forms.Screen.PrimaryScreen;
        if (screen is null) return false;

        var centerX = screen.Bounds.Left + screen.Bounds.Width / 2;
        var left = centerX - HotZoneWindow.ZoneWidth / 2;

        return pointer.X >= left
               && pointer.X <= left + HotZoneWindow.ZoneWidth
               && pointer.Y >= screen.Bounds.Top
               && pointer.Y <= screen.Bounds.Top + HotZoneWindow.ZoneHeight;
    }

    private void ExitApplication()
    {
        _hotZoneWindow?.Close();
        _hotZoneWindow = null;

        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose = true;
            _mainWindow.Close();
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _updateCancellation?.Cancel();
        _updateCancellation?.Dispose();
        _updateCancellation = null;

        if (_updateTimer is not null)
        {
            _updateTimer.Stop();
            _updateTimer.Tick -= UpdateTimer_Tick;
            _updateTimer = null;
        }

        if (_pointerTimer is not null)
        {
            _pointerTimer.Stop();
            _pointerTimer.Tick -= PointerTimer_Tick;
            _pointerTimer = null;
        }

        if (_notificationCenterService is not null)
        {
            _notificationCenterService.NotificationReceived -= NotificationCenterService_NotificationReceived;
        }
        _notificationPopupWindow?.Dispose();
        _notificationPopupWindow = null;
        _notificationCenterService = null;

        _trayService?.Dispose();
        _hotZoneWindow?.Close();
        _hotZoneWindow = null;
        _container?.Dispose();
        base.OnExit(e);
    }
}
