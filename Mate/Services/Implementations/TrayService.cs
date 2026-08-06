using System;
using System.Collections.Generic;
using Mate.Services.Interfaces;
using Forms = System.Windows.Forms;

namespace Mate.Services.Implementations;

public sealed class TrayService : ITrayService
{
    private readonly IThemeService _themeService;
    private readonly IAutoStartService _autoStartService;
    private readonly Dictionary<AppTheme, Forms.ToolStripMenuItem> _themeItems = new();
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _menu;
    private Forms.ToolStripMenuItem? _autoStartItem;
    private System.Drawing.Icon? _trayIcon;

    public TrayService(IThemeService themeService, IAutoStartService autoStartService)
    {
        _themeService = themeService;
        _autoStartService = autoStartService;
        _themeService.ThemeChanged += ThemeService_ThemeChanged;
    }

    public void Initialize(Action togglePanel, Action exitApplication)
    {
        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add("Открыть Mate", null, (_, _) => togglePanel());
        _menu.Items.Add(new Forms.ToolStripSeparator());

        var themeMenu = new Forms.ToolStripMenuItem("Тема");
        foreach (var option in _themeService.AvailableThemes)
        {
            var item = new Forms.ToolStripMenuItem(option.DisplayName)
            {
                CheckOnClick = false
            };
            item.Click += (_, _) => _themeService.SetTheme(option.Theme);
            _themeItems[option.Theme] = item;
            themeMenu.DropDownItems.Add(item);
        }
        UpdateThemeChecks();
        _menu.Items.Add(themeMenu);

        _autoStartItem = new Forms.ToolStripMenuItem("Автозапуск")
        {
            CheckOnClick = false,
            Checked = _autoStartService.IsEnabled
        };
        _autoStartItem.Click += AutoStartItem_Click;
        _menu.Items.Add(_autoStartItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());

        _menu.Items.Add("Выход", null, (_, _) => exitApplication());

        _trayIcon = LoadTrayIcon();
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

    public void Dispose()
    {
        _themeService.ThemeChanged -= ThemeService_ThemeChanged;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _menu?.Dispose();
        _menu = null;
        _autoStartItem = null;
        _themeItems.Clear();
    }

    private void ThemeService_ThemeChanged(object? sender, EventArgs e) => UpdateThemeChecks();

    private void AutoStartItem_Click(object? sender, EventArgs e)
    {
        if (_autoStartItem is null) return;

        var shouldEnable = !_autoStartService.IsEnabled;
        if (_autoStartService.SetEnabled(shouldEnable))
        {
            _autoStartItem.Checked = shouldEnable;
            return;
        }

        _autoStartItem.Checked = _autoStartService.IsEnabled;
        _notifyIcon?.ShowBalloonTip(
            3000,
            "Mate",
            "Не удалось изменить настройку автозапуска.",
            Forms.ToolTipIcon.Warning);
    }

    private void UpdateThemeChecks()
    {
        foreach (var pair in _themeItems)
        {
            pair.Value.Checked = pair.Key == _themeService.CurrentTheme;
        }
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/MateTray.ico", UriKind.Absolute));
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

        return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
    }
}
