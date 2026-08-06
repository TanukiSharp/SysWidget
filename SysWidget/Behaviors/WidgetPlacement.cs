using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using SysWidget.Interop;

namespace SysWidget.Behaviors;

/// <summary>
/// Attached behavior that keeps the widget window borderless, topmost, out of Alt-Tab, and
/// draggable, persisting its position through two-way-bound attached properties. The window's
/// code-behind stays empty and the view model stays UI-free; the small amount of imperative
/// Win32 glue (tool-window style) and the drag handling live here, their idiomatic WPF home.
/// </summary>
public static class WidgetPlacement
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(WidgetPlacement),
        new PropertyMetadata(false, OnEnabledChanged));

    public static readonly DependencyProperty WindowLeftProperty = DependencyProperty.RegisterAttached(
        "WindowLeft", typeof(double?), typeof(WidgetPlacement),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty WindowTopProperty = DependencyProperty.RegisterAttached(
        "WindowTop", typeof(double?), typeof(WidgetPlacement),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ResetPositionTokenProperty = DependencyProperty.RegisterAttached(
        "ResetPositionToken", typeof(int), typeof(WidgetPlacement),
        new PropertyMetadata(0, OnResetPositionTokenChanged));

    private static readonly DependencyProperty ControllerProperty = DependencyProperty.RegisterAttached(
        "Controller", typeof(Controller), typeof(WidgetPlacement), new PropertyMetadata(null));

    public static void SetEnabled(DependencyObject o, bool value) => o.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);

    public static void SetWindowLeft(DependencyObject o, double? value) => o.SetValue(WindowLeftProperty, value);
    public static double? GetWindowLeft(DependencyObject o) => (double?)o.GetValue(WindowLeftProperty);

    public static void SetWindowTop(DependencyObject o, double? value) => o.SetValue(WindowTopProperty, value);
    public static double? GetWindowTop(DependencyObject o) => (double?)o.GetValue(WindowTopProperty);

    public static void SetResetPositionToken(DependencyObject o, int value) => o.SetValue(ResetPositionTokenProperty, value);
    public static int GetResetPositionToken(DependencyObject o) => (int)o.GetValue(ResetPositionTokenProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window)
        {
            return;
        }

        if (e.NewValue is true)
        {
            window.SetValue(ControllerProperty, new Controller(window));
        }
        else if (window.GetValue(ControllerProperty) is Controller existing)
        {
            existing.Detach();
            window.ClearValue(ControllerProperty);
        }
    }

    private static void OnResetPositionTokenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d.GetValue(ControllerProperty) is Controller controller)
        {
            controller.ResetToDefault();
        }
    }

    /// <summary>Per-window state and the imperative placement/drag logic.</summary>
    private sealed class Controller
    {
        private readonly Window _window;

        // Held for the lifetime of the hook so the delegate is not garbage-collected.
        private readonly NativeMethods.WinEventDelegate _foregroundCallback;

        // SystemEvents exposes static events: the handlers must be held and unsubscribed
        // explicitly, or the controller (and the window) outlives the widget.
        private readonly PowerModeChangedEventHandler _powerModeHandler;
        private readonly EventHandler _displaySettingsHandler;
        private readonly DispatcherTimer _resettleTimer;

        private IntPtr _hwnd;
        private IntPtr _foregroundHook;
        private bool _initialized;
        private bool _systemEventsAttached;

        public Controller(Window window)
        {
            _window = window;
            _foregroundCallback = OnForegroundChanged;
            _powerModeHandler = OnPowerModeChanged;
            _displaySettingsHandler = OnDisplaySettingsChanged;

            _resettleTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1),
            };
            _resettleTimer.Tick += OnResettleTick;

            _window.SourceInitialized += OnSourceInitialized;
            _window.PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
            _window.Closed += (_, _) => Detach();
        }

        public void Detach()
        {
            _resettleTimer.Stop();

            if (_systemEventsAttached)
            {
                SystemEvents.PowerModeChanged -= _powerModeHandler;
                SystemEvents.DisplaySettingsChanged -= _displaySettingsHandler;
                _systemEventsAttached = false;
            }

            if (_foregroundHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_foregroundHook);
                _foregroundHook = IntPtr.Zero;
            }

            _window.SourceInitialized -= OnSourceInitialized;
            _window.PreviewMouseLeftButtonDown -= OnMouseLeftButtonDown;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(_window).Handle;

            // Keep the widget out of Alt-Tab while remaining clickable/draggable.
            IntPtr exStyle = NativeMethods.GetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE);
            IntPtr updated = (IntPtr)(exStyle.ToInt64() | NativeMethods.WS_EX_TOOLWINDOW);
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWL_EXSTYLE, updated);

            _initialized = true;
            ApplyPosition();

            HookForeground();

            SystemEvents.PowerModeChanged += _powerModeHandler;
            SystemEvents.DisplaySettingsChanged += _displaySettingsHandler;
            _systemEventsAttached = true;
        }

        /// <summary>
        /// Installs the foreground hook that re-asserts topmost whenever the foreground window
        /// changes. Opening the taskbar's overflow flyout (the "^" tray) or another topmost window
        /// can otherwise push the widget behind it; this brings it straight back on top.
        /// Event-driven, so no polling. Idempotent: any previous hook is released first.
        /// </summary>
        private void HookForeground()
        {
            if (_foregroundHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_foregroundHook);
                _foregroundHook = IntPtr.Zero;
            }

            _foregroundHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _foregroundCallback, 0, 0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
        }

        private void OnForegroundChanged(
            IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (!_initialized || _hwnd == IntPtr.Zero)
            {
                return;
            }

            // The event is delivered on this (UI) thread via the message loop, but some sources
            // (notably clicking the taskbar) re-order the z-order *after* this callback returns,
            // which would bury the widget again with no further event to recover. Defer the
            // re-assert to the tail of the message queue so it runs once the shell has settled.
            _window.Dispatcher.BeginInvoke(DispatcherPriority.Background, ReassertTopmost);
        }

        /// <summary>
        /// Forces the widget to the very top of the topmost band. A plain
        /// <c>SetWindowPos(HWND_TOPMOST)</c> on an already-topmost window is frequently a no-op
        /// (it will not re-order it above other topmost windows), so toggle through
        /// <c>HWND_NOTOPMOST</c> first — that transition always takes effect.
        /// </summary>
        private void ReassertTopmost()
        {
            if (!_initialized || _hwnd == IntPtr.Zero)
            {
                return;
            }

            const uint flags = NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE;
            NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0, flags);
            NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, flags);
        }

        // --- Resume from sleep / display changes ---

        private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume)
            {
                return;
            }

            ScheduleResettle();
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            ScheduleResettle();
        }

        /// <summary>
        /// Queues a single re-settle shortly after the system stops moving. Both events above are
        /// raised on the <see cref="SystemEvents"/> private thread, and a resume produces a burst of
        /// them while the shell is still rebuilding monitors and z-order — so hop to the UI thread
        /// and restart a one-shot timer, which coalesces the burst into one pass on settled state.
        /// </summary>
        private void ScheduleResettle()
        {
            _window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _resettleTimer.Stop();
                _resettleTimer.Start();
            }));
        }

        private void OnResettleTick(object? sender, EventArgs e)
        {
            _resettleTimer.Stop();

            if (!_initialized || _hwnd == IntPtr.Zero)
            {
                return;
            }

            // The hook is owned by the session's desktop, which can be rebuilt across a suspend;
            // re-installing it is cheap and idempotent, and a dropped hook would silently cost the
            // widget its topmost recovery for the rest of the session.
            HookForeground();
            ClampIntoView();
            ReassertTopmost();
        }

        /// <summary>
        /// Brings the window back on screen if its saved spot no longer exists — a monitor
        /// unplugged (or a resolution change) while asleep leaves it parked in dead space, which
        /// looks exactly like the widget having disappeared.
        /// </summary>
        private void ClampIntoView()
        {
            Rect virtualScreen = new(
                SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

            Rect bounds = new(_window.Left, _window.Top, _window.ActualWidth, _window.ActualHeight);
            if (virtualScreen.IntersectsWith(bounds))
            {
                return;
            }

            ResetToDefault();
        }

        private void ApplyPosition()
        {
            if (!_initialized)
            {
                return;
            }

            double? left = GetWindowLeft(_window);
            double? top = GetWindowTop(_window);

            if (left is null || top is null)
            {
                (left, top) = DefaultPosition();
            }

            _window.Topmost = true;
            _window.Left = left.Value;
            _window.Top = top.Value;
        }

        /// <summary>
        /// Snaps the window back to the default top-right spot and re-asserts topmost.
        /// Recovery hatch for when another app (e.g. Magnifier) relocates or buries it.
        /// </summary>
        public void ResetToDefault()
        {
            if (!_initialized)
            {
                return;
            }

            (double left, double top) = DefaultPosition();
            _window.Topmost = true;
            _window.Left = left;
            _window.Top = top;
            SetWindowLeft(_window, left);
            SetWindowTop(_window, top);
        }

        private (double Left, double Top) DefaultPosition()
        {
            // Near the top-right of the primary work area.
            Rect work = SystemParameters.WorkArea;
            return (work.Right - _window.ActualWidth - 16, work.Top + 16);
        }

        private void OnMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            _window.DragMove();

            // Persist the new position back through the two-way bindings.
            SetWindowLeft(_window, _window.Left);
            SetWindowTop(_window, _window.Top);
        }
    }
}
