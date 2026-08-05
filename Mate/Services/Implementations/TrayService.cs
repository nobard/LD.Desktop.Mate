using System;
using System.Collections.Generic;
using Mate.Services.Interfaces;
using Forms = System.Windows.Forms;

namespace Mate.Services.Implementations;

public sealed class TrayService : ITrayService
{
    private readonly IThemeService _themeService;
    private readonly Dictionary<AppTheme, Forms.ToolStripMenuItem> _themeItems = new();
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _menu;

    public TrayService(IThemeService themeService)
    {
        _themeService = themeService;
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
        _menu.Items.Add(new Forms.ToolStripSeparator());

        _menu.Items.Add("Выход", null, (_, _) => exitApplication());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
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
        _menu?.Dispose();
        _menu = null;
        _themeItems.Clear();
    }

    private void ThemeService_ThemeChanged(object? sender, EventArgs e) => UpdateThemeChecks();

    private void UpdateThemeChecks()
    {
        foreach (var pair in _themeItems)
        {
            pair.Value.Checked = pair.Key == _themeService.CurrentTheme;
        }
    }
}
