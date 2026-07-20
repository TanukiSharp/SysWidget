using System.Windows;
using System.Windows.Threading;

namespace SysWidget.Behaviors;

/// <summary>
/// Makes an element's width grow-only: whenever its content gets wider the element's
/// <see cref="FrameworkElement.MinWidth"/> ratchets up to match, so a later, narrower value
/// can never make it shrink. This stabilizes the widget layout (it stops jittering as
/// numbers change). Bumping <see cref="ResetTokenProperty"/> clears the ratchet, letting the
/// element re-measure from its current content — the escape hatch for a one-off huge value
/// that left the widget stuck too wide.
/// </summary>
public static class RatchetWidth
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(RatchetWidth),
        new PropertyMetadata(false, OnEnabledChanged));

    public static readonly DependencyProperty ResetTokenProperty = DependencyProperty.RegisterAttached(
        "ResetToken", typeof(int), typeof(RatchetWidth),
        new PropertyMetadata(0, OnResetTokenChanged));

    public static void SetEnabled(DependencyObject o, bool value) => o.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);

    public static void SetResetToken(DependencyObject o, int value) => o.SetValue(ResetTokenProperty, value);
    public static int GetResetToken(DependencyObject o) => (int)o.GetValue(ResetTokenProperty);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if (e.NewValue is true)
        {
            element.SizeChanged += OnSizeChanged;
            element.Loaded += OnLoaded;
        }
        else
        {
            element.SizeChanged -= OnSizeChanged;
            element.Loaded -= OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnLoaded;

        // The first layout pass can transiently measure not-yet-collapsed chrome — notably the
        // leading separator, whose visibility binding hasn't resolved yet on the first item.
        // That would latch a too-wide MinWidth forever (the ratchet never shrinks). Clear it
        // once, after layout has settled, so the real content width becomes the baseline.
        element.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            element.MinWidth = 0;
        }));
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement element && e.NewSize.Width > element.MinWidth)
        {
            element.MinWidth = e.NewSize.Width;
        }
    }

    private static void OnResetTokenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
            element.MinWidth = 0;
        }
    }
}
