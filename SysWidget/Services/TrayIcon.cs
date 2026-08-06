using System.Runtime.InteropServices;
using SysWidget.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace SysWidget.Services;

/// <summary>
/// The single system-tray icon and its menu. Imperative WinForms glue that drives the
/// shared <see cref="WidgetViewModel"/>; check states are refreshed from the view model
/// each time the menu opens, so they always reflect current state.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WidgetViewModel _vm;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _componentsItem;
    private readonly Forms.ToolStripMenuItem _themeItem;
    private readonly Forms.ToolStripMenuItem _startupItem;

    public TrayIcon(WidgetViewModel vm)
    {
        _vm = vm;

        Forms.ContextMenuStrip menu = new();

        _componentsItem = new Forms.ToolStripMenuItem("Components");
        _themeItem = new Forms.ToolStripMenuItem("Dark theme", null, (_, _) => _vm.ToggleThemeCommand.Execute(null));
        _startupItem = new Forms.ToolStripMenuItem("Start with Windows", null, (_, _) => _vm.StartWithWindows = !_vm.StartWithWindows);

        Forms.ToolStripMenuItem resetSizeItem = new("Reset size", null, (_, _) => _vm.ResetSizeCommand.Execute(null));
        Forms.ToolStripMenuItem resetPositionItem = new("Reset position", null, (_, _) => _vm.ResetPositionCommand.Execute(null));
        Forms.ToolStripMenuItem aboutItem = new("About SysWidget…", null, (_, _) => _vm.AboutCommand.Execute(null));
        Forms.ToolStripMenuItem quitItem = new("Quit", null, (_, _) => _vm.QuitCommand.Execute(null));

        foreach (ComponentToggleViewModel toggle in _vm.AvailableComponents)
        {
            ComponentToggleViewModel captured = toggle;
            Forms.ToolStripMenuItem item = new(captured.DisplayName)
            {
                CheckOnClick = true,
                Checked = captured.IsActive,
                Tag = captured,
            };
            item.CheckedChanged += (_, _) => captured.IsActive = item.Checked;
            _componentsItem.DropDownItems.Add(item);
        }

        menu.Items.Add(_componentsItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_themeItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(resetSizeItem);
        menu.Items.Add(resetPositionItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(aboutItem);
        menu.Items.Add(quitItem);

        menu.Opening += (_, _) => RefreshChecks();

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "SysWidget",
            Visible = true,
            Icon = CreateIcon(),
            ContextMenuStrip = menu,
        };
    }

    private void RefreshChecks()
    {
        _themeItem.Checked = _vm.IsDark;
        _startupItem.Checked = _vm.StartWithWindows;

        foreach (Forms.ToolStripItem item in _componentsItem.DropDownItems)
        {
            if (item is Forms.ToolStripMenuItem mi && mi.Tag is ComponentToggleViewModel toggle)
            {
                mi.Checked = toggle.IsActive;
            }
        }
    }

    /// <summary>Draws a tiny three-bar chart glyph so no .ico asset is needed.</summary>
    private static Drawing.Icon CreateIcon()
    {
        using Drawing.Bitmap bmp = new(16, 16);
        using (Drawing.Graphics g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.None;
            g.Clear(Drawing.Color.Transparent);
            using Drawing.Brush brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(230, 240, 240, 240));
            g.FillRectangle(brush, 1, 9, 3, 6);
            g.FillRectangle(brush, 6, 5, 3, 10);
            g.FillRectangle(brush, 11, 2, 3, 13);
        }

        IntPtr handle = bmp.GetHicon();
        try
        {
            using Drawing.Icon temp = Drawing.Icon.FromHandle(handle);
            return (Drawing.Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
