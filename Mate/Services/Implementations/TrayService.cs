using System;
using Mate.Services.Interfaces;
using Forms = System.Windows.Forms;

namespace Mate.Services.Implementations;

public sealed class TrayService : ITrayService
{
    private Forms.NotifyIcon? _notifyIcon;

    public void Initialize(Action togglePanel, Action exitApplication)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть Mate", null, (_, _) => togglePanel());
        menu.Items.Add(new Forms.ToolStripSeparator());

        var themeMenu = new Forms.ToolStripMenuItem("Тема");
        var darkThemeItem = new Forms.ToolStripMenuItem("Тёмная")
        {
            Checked = true,
            CheckOnClick = false
        };
        darkThemeItem.Click += (_, _) => darkThemeItem.Checked = true;
        themeMenu.DropDownItems.Add(darkThemeItem);
        menu.Items.Add(themeMenu);
        menu.Items.Add(new Forms.ToolStripSeparator());

        menu.Items.Add("Выход", null, (_, _) => exitApplication());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Mate",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left) togglePanel();
        };
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _notifyIcon = null;
    }
}
