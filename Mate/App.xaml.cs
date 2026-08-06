using System;
using System.Windows;
using System.Windows.Threading;
using Autofac;
using Mate.MVVM.Views;
using Mate.Services.DI;
using Mate.Services.Interfaces;
using Forms = System.Windows.Forms;

namespace Mate;

public partial class App : Application
{
    private IContainer? _container;
    private ITrayService? _trayService;
    private MainWindow? _mainWindow;
    private HotZoneWindow? _hotZoneWindow;
    private DispatcherTimer? _pointerTimer;
    private DateTime _panelShownAtUtc;
    private DateTime? _pointerLeftAtUtc;
    private bool _pointerEnteredPanel;

    private static readonly TimeSpan InitialPointerGracePeriod = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(220);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _container = AutofacConfig.GetConfiguredContainer();
        _mainWindow = _container.Resolve<MainWindow>();
        _mainWindow.ApplyScreenScale();
        MainWindow = _mainWindow;

        _hotZoneWindow = new HotZoneWindow();
        _hotZoneWindow.ApplyScale(_mainWindow.InterfaceScale);
        _hotZoneWindow.PositionAtTopCenter();
        _hotZoneWindow.Show();

        _trayService = _container.Resolve<ITrayService>();
        _trayService.Initialize(
            () => Dispatcher.Invoke(TogglePanel),
            () => Dispatcher.Invoke(ExitApplication));

        _pointerTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(70)
        };
        _pointerTimer.Tick += PointerTimer_Tick;
        _pointerTimer.Start();
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
        if (_pointerTimer is not null)
        {
            _pointerTimer.Stop();
            _pointerTimer.Tick -= PointerTimer_Tick;
            _pointerTimer = null;
        }

        _trayService?.Dispose();
        _hotZoneWindow?.Close();
        _hotZoneWindow = null;
        _container?.Dispose();
        base.OnExit(e);
    }
}
