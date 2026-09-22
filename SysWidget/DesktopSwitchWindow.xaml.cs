using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SysWidget.Interop;
using Forms = System.Windows.Forms;
using Media = System.Windows.Media;

namespace SysWidget;

/// <summary>
/// The big "1 → 2" banner shown for one virtual desktop move, on one monitor. One instance is
/// created per monitor per switch and closes itself when the fade completes — a window is bound to
/// the desktop that was current when it was created, so a pooled, re-shown window would stay
/// stranded on the desktop it was born on.
/// </summary>
public partial class DesktopSwitchWindow : Window
{
    private const int MaxAdjustPasses = 4;

    private static readonly Media.Color DarkTextColor = Media.Color.FromRgb(0xF0, 0xF0, 0xF0);
    private static readonly Media.Color LightTextColor = Media.Color.FromRgb(0x20, 0x20, 0x20);

    // #007ACC lightened — same hue and saturation, lightness 40% -> 70% — so the destination
    // still reads as blue against the pill without going dim.
    private static readonly Media.Color DestinationColor = Media.Color.FromRgb(0x66, 0xC3, 0xFF);

    private static readonly Media.Brush DarkPill = Frozen(Media.Color.FromArgb(0x8C, 0x1E, 0x1E, 0x1E));
    private static readonly Media.Brush LightPill = Frozen(Media.Color.FromArgb(0x8C, 0xF4, 0xF4, 0xF4));
    private static readonly Media.Brush DarkText = Frozen(DarkTextColor);
    private static readonly Media.Brush LightText = Frozen(LightTextColor);
    private static readonly Media.Brush DestinationText = Frozen(DestinationColor);

    private readonly Forms.Screen _screen;
    private readonly double _sizePercent;
    private readonly TimeSpan _hold;
    private readonly TimeSpan _fade;
    private readonly Guid _desktopId;
    private int _pass;
    private bool _closing;

    /// <param name="from">Desktop left behind.</param>
    /// <param name="to">Desktop arrived at.</param>
    /// <param name="screen">Monitor to centre on, in physical pixels.</param>
    /// <param name="isDark">Widget theme, so the banner matches the widget.</param>
    /// <param name="sizePercent">Banner height as a fraction of the monitor's smaller side.</param>
    /// <param name="desktopId">Desktop the banner belongs to; used to correct a mis-parented window.</param>
    public DesktopSwitchWindow(
        int from, int to, Forms.Screen screen, bool isDark, double sizePercent,
        TimeSpan hold, TimeSpan fade, Guid desktopId)
    {
        InitializeComponent();

        _screen = screen;
        _sizePercent = sizePercent;
        _hold = hold;
        _fade = fade;
        _desktopId = desktopId;

        Pill.Background = isDark ? DarkPill : LightPill;
        WriteCaption(from, to, isDark);

        SourceInitialized += OnSourceInitializedCore;
        Loaded += OnLoadedCore;
        ContentRendered += OnContentRenderedCore;
    }

    /// <summary>
    /// Lays the move out spatially: the lower index stays on the left and the arrow points at the
    /// destination, so 1 → 2 going forward and 3 ← 4 going back. The destination is the one number
    /// that matters, so it alone is coloured, and the arrow fades from the origin's colour to the
    /// destination's — which makes the direction readable even before the glyph registers.
    /// </summary>
    private void WriteCaption(int from, int to, bool isDark)
    {
        Media.Color originColor = isDark ? DarkTextColor : LightTextColor;
        Media.Brush originBrush = isDark ? DarkText : LightText;

        string origin = from.ToString(CultureInfo.InvariantCulture);
        string destination = to.ToString(CultureInfo.InvariantCulture);

        if (to > from)
        {
            LeftNumber.Text = origin;
            LeftNumber.Foreground = originBrush;
            RightNumber.Text = destination;
            RightNumber.Foreground = DestinationText;
            Arrow.Text = "→";
            Arrow.Foreground = Gradient(originColor, DestinationColor);
            return;
        }

        LeftNumber.Text = destination;
        LeftNumber.Foreground = DestinationText;
        RightNumber.Text = origin;
        RightNumber.Foreground = originBrush;
        Arrow.Text = "←";
        Arrow.Foreground = Gradient(DestinationColor, originColor);
    }

    /// <summary>A left-to-right gradient across whichever element it is applied to.</summary>
    private static Media.Brush Gradient(Media.Color left, Media.Color right)
    {
        Media.LinearGradientBrush brush = new()
        {
            StartPoint = new System.Windows.Point(0.0, 0.5),
            EndPoint = new System.Windows.Point(1.0, 0.5),
        };

        brush.GradientStops.Add(new Media.GradientStop(left, 0.0));
        brush.GradientStops.Add(new Media.GradientStop(right, 1.0));
        brush.Freeze();
        return brush;
    }

    private void OnSourceInitializedCore(object? sender, EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        // WS_EX_TRANSPARENT makes the whole window click-through — the exact opposite of the
        // widget, which paints a near-zero alpha precisely to stay hit-testable. NOACTIVATE keeps
        // focus where it is, TOOLWINDOW keeps the banner out of Alt-Tab.
        long style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));

        // Park it well inside the target monitor before anything is measured, so the window is
        // already in that monitor's DPI context by the time layout runs.
        System.Drawing.Rectangle bounds = _screen.Bounds;
        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOPMOST,
            bounds.X + (bounds.Width / 4), bounds.Y + (bounds.Height / 4), 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private void OnLoadedCore(object? sender, RoutedEventArgs e)
    {
        // First guess at the banner height. It is expressed in device-independent pixels while the
        // target is in real ones, so it is only right at 100% scaling; Adjust() converges on the
        // rest without ever needing to know the monitor's scale factor.
        Scaler.Height = TargetPixels;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        VirtualDesktopManager.EnsureOnDesktop(hwnd, _desktopId);
    }

    private void OnContentRenderedCore(object? sender, EventArgs e)
    {
        Adjust();
    }

    private double TargetPixels
    {
        get
        {
            System.Drawing.Rectangle bounds = _screen.Bounds;
            return Math.Min(bounds.Width, bounds.Height) * _sizePercent;
        }
    }

    /// <summary>
    /// Sizes the banner to <see cref="_sizePercent"/> of its monitor's smaller side and centres it,
    /// measuring the real window rect rather than converting between device-independent and real
    /// pixels. Per-Monitor V2 changes that conversion factor under our feet — a window moved to
    /// another monitor is relaid out after the fact — so instead of chasing it, each pass corrects
    /// from what actually came out and reschedules itself until the size stops moving. The banner
    /// stays invisible until then, so none of this settling shows.
    /// </summary>
    private void Adjust()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        double target = TargetPixels;
        bool settled = true;

        if (NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT rect))
        {
            int height = rect.Bottom - rect.Top;
            if (height > 0 && Math.Abs(height - target) > 2.0)
            {
                Scaler.Height *= target / height;
                UpdateLayout();
                settled = false;
            }
        }

        _pass++;
        if (!settled && _pass < MaxAdjustPasses)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Adjust));
            return;
        }

        // Freeze the size before placing the window: while SizeToContent is on, any further
        // relayout resizes the window through WPF, which reasserts its own idea of where the
        // window belongs. The final centring is then deferred one dispatcher turn so it runs after
        // WPF has had its last say on the matter.
        SizeToContent = SizeToContent.Manual;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Place));
    }

    private void Place()
    {
        if (_closing)
        {
            return;
        }

        CenterOnMonitor(new WindowInteropHelper(this).Handle);
        Pill.Opacity = 1.0;
        StartFade();
    }

    /// <summary>
    /// Centres the window on its monitor from its real rect in physical pixels — no conversion
    /// involved, so it lands right whatever scale factor the monitor runs at.
    /// </summary>
    private void CenterOnMonitor(IntPtr hwnd)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT rect))
        {
            return;
        }

        System.Drawing.Rectangle bounds = _screen.Bounds;
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOPMOST,
            bounds.X + ((bounds.Width - width) / 2), bounds.Y + ((bounds.Height - height) / 2), 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private void StartFade()
    {
        DoubleAnimation fade = new()
        {
            From = 1.0,
            To = 0.0,
            BeginTime = _hold,
            Duration = new Duration(_fade),
            FillBehavior = FillBehavior.HoldEnd,
        };

        fade.Completed += (_, _) => CloseSafely();
        Pill.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>Closes once, whether the fade finished or the service tore the banner down early.</summary>
    public void CloseSafely()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Close();
    }

    private static Media.Brush Frozen(Media.Color color)
    {
        Media.SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }
}
