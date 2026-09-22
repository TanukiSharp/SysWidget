using System.Windows.Threading;
using SysWidget.Settings;
using Forms = System.Windows.Forms;

namespace SysWidget.Services;

/// <summary>
/// Turns <see cref="VirtualDesktopWatcher.Switched"/> events into one big banner per monitor.
/// Lives for the whole app lifetime and is independent of the optional "vdesk" widget component.
/// </summary>
public sealed class DesktopSwitchOverlay : IDisposable
{
    private readonly AppSettings _settings;
    private readonly SynchronizationContext _sync;
    private readonly Action<DesktopSwitch> _handler;
    private readonly DispatcherTimer _debounce;
    private readonly List<DesktopSwitchWindow> _open = [];
    private int _pendingFrom;
    private int _pendingTo;
    private bool _hasPending;
    private bool _disposed;

    public DesktopSwitchOverlay(AppSettings settings, SynchronizationContext sync)
    {
        _settings = settings;
        _sync = sync;
        _debounce = new DispatcherTimer();
        _debounce.Tick += (_, _) => Flush();
        _handler = OnSwitched;
        VirtualDesktopWatcher.Switched += _handler;
    }

    private void OnSwitched(DesktopSwitch move)
    {
        // Raised on the watcher thread; everything below touches WPF.
        _sync.Post(_ => Accumulate(move), null);
    }

    /// <summary>
    /// Folds a move into the pending one and restarts the wait. Holding the shortcut down walks
    /// through desktops faster than a banner can be built, and on a busy machine putting one up per
    /// step is both wasteful and ugly — so only the trip as a whole is ever shown: 1→2, 2→3, 3→4
    /// becomes a single "1 → 4" once the switching stops.
    /// </summary>
    private void Accumulate(DesktopSwitch move)
    {
        try
        {
            if (_disposed || !_settings.ShowDesktopSwitch)
            {
                return;
            }

            if (!_hasPending)
            {
                _pendingFrom = move.From;
                _hasPending = true;
            }

            _pendingTo = move.To;

            _debounce.Stop();
            _debounce.Interval = TimeSpan.FromSeconds(_settings.DesktopSwitchDelaySeconds);
            _debounce.Start();
        }
        catch (Exception)
        {
        }
    }

    private void Flush()
    {
        _debounce.Stop();

        if (!_hasPending)
        {
            return;
        }

        _hasPending = false;
        int from = _pendingFrom;
        int to = _pendingTo;

        // A round trip ends where it started: there is nothing to announce.
        if (from == to)
        {
            return;
        }

        Show(from, to);
    }

    private void Show(int from, int to)
    {
        try
        {
            if (_disposed || !_settings.ShowDesktopSwitch)
            {
                return;
            }

            // A new banner replaces the one still fading out rather than stacking on top of it.
            CloseOpen();

            bool isDark = _settings.Theme == WidgetTheme.Dark;
            TimeSpan hold = TimeSpan.FromSeconds(_settings.DesktopSwitchHoldSeconds);
            TimeSpan fade = TimeSpan.FromSeconds(_settings.DesktopSwitchFadeSeconds);
            Guid desktopId = VirtualDesktopWatcher.Current.Id;

            foreach (Forms.Screen screen in Forms.Screen.AllScreens)
            {
                DesktopSwitchWindow window = new(
                    from, to, screen, isDark, _settings.DesktopSwitchSizePercent, hold, fade, desktopId);

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
        _debounce.Stop();
        CloseOpen();
    }
}
