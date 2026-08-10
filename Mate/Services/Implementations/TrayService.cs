using System;
using System.Windows;
using Mate.MVVM.ViewModels;
using Mate.MVVM.Views;
using Mate.Services.Interfaces;
using Forms = System.Windows.Forms;

namespace Mate.Services.Implementations;

public sealed class TrayService : ITrayService
{
    private readonly IHoverActivationService _hoverActivationService;
    private Forms.NotifyIcon? _notifyIcon;
    private TrayMenuViewModel? _menuViewModel;
    private TrayMenuWindow? _menuWindow;
    private System.Drawing.Icon? _trayIcon;
    private System.Drawing.Icon? _updateTrayIcon;

    public TrayService(IHoverActivationService hoverActivationService) =>
        _hoverActivationService = hoverActivationService;

    public void Initialize(
        Action togglePanel,
        Action openSettings,
        Action exitApplication)
    {
        _menuViewModel = new TrayMenuViewModel(
            _hoverActivationService,
            togglePanel,
            openSettings,
            exitApplication);
        _menuWindow = new TrayMenuWindow(_menuViewModel);

        _trayIcon = LoadTrayIcon("MateTray.ico");
        _updateTrayIcon = LoadTrayIcon("MateTrayUpdate.ico", _trayIcon);
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "Mate",
            Visible = true
        };
        _notifyIcon.MouseClick += NotifyIcon_MouseClick;

        void NotifyIcon_MouseClick(object? sender, Forms.MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                togglePanel();
                return;
            }

            if (eventArgs.Button != Forms.MouseButtons.Right) return;
            Application.Current.Dispatcher.BeginInvoke(() => _menuWindow?.ShowMenu());
        }
    }

    public void SetUpdateCheckInProgress(bool isInProgress)
    {
        // Update progress is shown inside the application.
    }

    public void ShowUpdateAvailable(string version, Action installUpdate) =>
        SetUpdateIconState(hasUpdate: true);

    public void SetUpdateInstallationInProgress()
    {
        // Installation progress is shown inside the application.
    }

    public void ShowUpdateCheckMessage(string message) =>
        SetUpdateIconState(hasUpdate: false);

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        if (_menuWindow is not null)
        {
            _menuWindow.Close();
            _menuWindow = null;
        }

        _menuViewModel?.Dispose();
        _menuViewModel = null;

        _updateTrayIcon?.Dispose();
        _updateTrayIcon = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private void SetUpdateIconState(bool hasUpdate)
    {
        if (_notifyIcon is null || _trayIcon is null) return;

        if (!hasUpdate)
        {
            _notifyIcon.Icon = _trayIcon;
            _notifyIcon.Text = "Mate";
            return;
        }

        _notifyIcon.Icon = _updateTrayIcon ?? _trayIcon;
        _notifyIcon.Text = "Mate — доступно обновление";
    }

    private static System.Drawing.Icon LoadTrayIcon(
        string resourceName,
        System.Drawing.Icon? fallback = null)
    {
        try
        {
            var resource = Application.GetResourceStream(
                new Uri($"pack://application:,,,/Assets/{resourceName}", UriKind.Absolute));
            if (resource is not null)
            {
                using var stream = resource.Stream;
                using var icon = new System.Drawing.Icon(stream);
                return (System.Drawing.Icon)icon.Clone();
            }
        }
        catch
        {
            // Fall back to a system icon if the packaged resource cannot be read.
        }

        return fallback is not null
            ? (System.Drawing.Icon)fallback.Clone()
            : (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
    }
}
