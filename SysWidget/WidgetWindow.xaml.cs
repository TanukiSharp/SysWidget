using System.Windows;

namespace SysWidget;

/// <summary>
/// The widget window. Intentionally logic-free: placement, drag, and mode switching are
/// handled by <see cref="Behaviors.WidgetPlacement"/> via data-bound attached properties;
/// everything else is DataBinding to <see cref="ViewModels.WidgetViewModel"/>.
/// </summary>
public partial class WidgetWindow : Window
{
    public WidgetWindow()
    {
        InitializeComponent();
    }
}
