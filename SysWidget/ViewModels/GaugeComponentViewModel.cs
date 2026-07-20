namespace SysWidget.ViewModels;

/// <summary>
/// A component rendered as a vertical green-to-red bar (driven by <see cref="ComponentViewModel.Raw"/>)
/// plus its value text. Behaviorally identical to the base; the distinct type is what selects the
/// gauge DataTemplate in the view.
/// </summary>
public sealed class GaugeComponentViewModel : ComponentViewModel
{
    public GaugeComponentViewModel(string id) : base(id)
    {
    }
}
