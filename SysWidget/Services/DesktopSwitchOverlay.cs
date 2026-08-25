using SysWidget.Settings;
using Forms = System.Windows.Forms;

namespace SysWidget.Services;

/// <summary>
/// Turns a <see cref="VirtualDesktopWatcher.Switched"/> event into one big banner per monitor.
/// Lives for the whole app lifetime and is independent of the optional "vdesk" widget component.
/// </summary>
public sealed class DesktopSwitchOverlay : IDisposable
{
    private readonly AppSettings _settings;
    private readonly SynchronizationContext _sync;
    private readonly Action<DesktopSwitch> _handler;
    private readonly List<DesktopSwitchWindow> _open = [];
    private bool _disposed;

    public DesktopSwitchOverlay(AppSettings settings, SynchronizationContext sync)
    {
        _settings = settings;
        _sync = sync;
        _handler = OnSwitched;
        VirtualDesktopWatcher.Switched += _handler;
    }

    /// <summary>
    /// Formats a move so the arrow points at the destination while the lower index stays on the
    /// left: 1 → 2 going forward, 3 ← 4 going back.
    /// </summary>
    public static string Format(int from, int to)
    {
        if (to > from)
        {
            return $"{from} → {to}";
        }

        return $"{to} ← {from}";
    }

    private void OnSwitched(DesktopSwitch move)
    {
        // Raised on the watcher thread; everything below touches WPF.
        _sync.Post(_ => Show(move), null);
    }

    private void Show(DesktopSwitch move)
    {
        try
        {
            if (_disposed || !_settings.ShowDesktopSwitch)
            {
                return;
            }

            // Coalesce: a rapid back-and-forth should replace the banner, not stack banners.
            CloseOpen();

            string text = Format(move.From, move.To);
            bool isDark = _settings.Theme == WidgetTheme.Dark;
            TimeSpan hold = TimeSpan.FromSeconds(_settings.DesktopSwitchHoldSeconds);
            TimeSpan fade = TimeSpan.FromSeconds(_settings.DesktopSwitchFadeSeconds);
            Guid desktopId = VirtualDesktopWatcher.Current.Id;

            foreach (Forms.Screen screen in Forms.Screen.AllScreens)
            {
                DesktopSwitchWindow window = new(
                    text, screen, isDark, _settings.DesktopSwitchSizePercent, hold, fade, desktopId);

                window.Closed += (s, _) =>
                {
                    if (s is DesktopSwitchWindow closed)
                    {
                        _open.Remove(closed);
                    }
                };

                _open.Add(window);
                window.Show();
            }
        }
        catch (Exception)
        {
            // Same stance as ComponentHost.SafeSample: a banner that fails to appear must never
            // take the widget down with it.
        }
    }

    private void CloseOpen()
    {
        // CloseSafely raises Closed, which mutates _open; iterate over a copy.
        DesktopSwitchWindow[] windows = [.. _open];
        _open.Clear();

        foreach (DesktopSwitchWindow window in windows)
        {
            try
            {
                window.CloseSafely();
            }
            catch (Exception)
            {
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        VirtualDesktopWatcher.Switched -= _handler;
        CloseOpen();
    }
}
