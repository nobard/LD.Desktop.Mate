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
    private Forms.ToolStripMenuItem? _updateItem;
    private Action? _checkForUpdatesAction;
    private Action? _installUpdateAction;
    private System.Drawing.Icon? _trayIcon;
    private System.Drawing.Icon? _updateTrayIcon;

    public TrayService(IThemeService themeService, IAutoStartService autoStartService)
    {
        _themeService = themeService;
        _autoStartService = autoStartService;
        _themeService.ThemeChanged += ThemeService_ThemeChanged;
    }

    public void Initialize(Action togglePanel, Action checkForUpdates, Action exitApplication)
    {
        _checkForUpdatesAction = checkForUpdates;
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

        _updateItem = new Forms.ToolStripMenuItem("Проверить обновления");
        _updateItem.Click += UpdateItem_Click;
        _menu.Items.Add(_updateItem);
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
        if (_updateItem is null) return;

        _updateItem.Enabled = !isInProgress;
        if (isInProgress)
        {
            _updateItem.Text = "Проверка обновлений…";
            _updateItem.ToolTipText = string.Empty;
        }
        else if (_installUpdateAction is null
                 && _updateItem.Text == "Проверка обновлений…")
        {
            _updateItem.Text = "Проверить обновления";
            _updateItem.ToolTipText = string.Empty;
        }
    }

    public void ShowUpdateAvailable(string version, Action installUpdate)
    {
        _installUpdateAction = installUpdate;
        SetUpdateIconState(hasUpdate: true);
        if (_updateItem is not null)
        {
            _updateItem.Enabled = true;
            _updateItem.Text = $"Доступна версия {version}";
            _updateItem.ToolTipText = "Скачать и установить обновление";
        }
    }

    public void SetUpdateInstallationInProgress()
    {
        if (_updateItem is null) return;

        _updateItem.Enabled = false;
        _updateItem.Text = "Скачивание обновления…";
        _updateItem.ToolTipText = string.Empty;
    }

    public void ShowUpdateCheckMessage(string message)
    {
        _installUpdateAction = null;
        SetUpdateIconState(hasUpdate: false);
        if (_updateItem is null) return;

        _updateItem.Enabled = true;
        _updateItem.Text = message;
        _updateItem.ToolTipText = "Нажмите, чтобы проверить снова";
    }

    public void Dispose()
    {
        _themeService.ThemeChanged -= ThemeService_ThemeChanged;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        _updateTrayIcon?.Dispose();
        _updateTrayIcon = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _menu?.Dispose();
        _menu = null;
        _autoStartItem = null;
        _updateItem = null;
        _checkForUpdatesAction = null;
        _installUpdateAction = null;
        _themeItems.Clear();
    }

    private void ThemeService_ThemeChanged(object? sender, EventArgs e) => UpdateThemeChecks();

    private void UpdateItem_Click(object? sender, EventArgs e)
    {
        var action = _installUpdateAction ?? _checkForUpdatesAction;
        action?.Invoke();
    }

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
