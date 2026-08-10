using System;
using Mate.Services.Interfaces;
using Forms = System.Windows.Forms;

namespace Mate.Services.Implementations;

public sealed class TrayService : ITrayService
{
    private readonly IHoverActivationService _hoverActivationService;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _menu;
    private Forms.ToolStripMenuItem? _hoverActivationItem;
    private System.Drawing.Icon? _trayIcon;
    private System.Drawing.Icon? _updateTrayIcon;

    public TrayService(IHoverActivationService hoverActivationService)
    {
        _hoverActivationService = hoverActivationService;
        _hoverActivationService.EnabledChanged += HoverActivationService_EnabledChanged;
    }

    public void Initialize(
        Action togglePanel,
        Action openSettings,
        Action exitApplication)
    {
        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add("Открыть Mate", null, (_, _) => togglePanel());
        _menu.Items.Add("Настройки", null, (_, _) => openSettings());
        _menu.Items.Add(new Forms.ToolStripSeparator());

        _hoverActivationItem = new Forms.ToolStripMenuItem("Отключить открытие по наведению")
        {
            CheckOnClick = false,
            Checked = !_hoverActivationService.IsEnabled
        };
        _hoverActivationItem.Click += HoverActivationItem_Click;
        _menu.Items.Add(_hoverActivationItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());

        _menu.Items.Add("Выход", null, (_, _) => exitApplication());

        _trayIcon = LoadTrayIcon("MateTray.ico");
        _updateTrayIcon = LoadTrayIcon("MateTrayUpdate.ico", _trayIcon);
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _trayIcon,
            Text = "Mate",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left) togglePanel();
        };
    }

    public void SetUpdateCheckInProgress(bool isInProgress)
    {
        // Update progress is shown inside the application.
    }

    public void ShowUpdateAvailable(string version, Action installUpdate)
    {
        SetUpdateIconState(hasUpdate: true);
    }

    public void SetUpdateInstallationInProgress()
    {
        // Installation progress is shown inside the application.
    }

    public void ShowUpdateCheckMessage(string message)
    {
        SetUpdateIconState(hasUpdate: false);
    }

    public void Dispose()
    {
        _hoverActivationService.EnabledChanged -= HoverActivationService_EnabledChanged;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        _updateTrayIcon?.Dispose();
        _updateTrayIcon = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _menu?.Dispose();
        _menu = null;
        _hoverActivationItem = null;
    }

    private void HoverActivationService_EnabledChanged(object? sender, EventArgs e)
    {
        if (_hoverActivationItem is not null)
        {
            _hoverActivationItem.Checked = !_hoverActivationService.IsEnabled;
        }
    }

    private void HoverActivationItem_Click(object? sender, EventArgs e) =>
        _hoverActivationService.SetEnabled(!_hoverActivationService.IsEnabled);

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
            var resource = System.Windows.Application.GetResourceStream(
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
