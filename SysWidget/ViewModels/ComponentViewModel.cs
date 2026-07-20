using SysWidget.Components;

namespace SysWidget.ViewModels;

/// <summary>
/// Binding surface for one active component. The view renders it via a DataTemplate
/// selected on this type (subclass + add a template to render a component differently,
/// e.g. a gauge — never bind a control to a component directly).
/// </summary>
public class ComponentViewModel : ViewModelBase
{
    private string _label = string.Empty;
    private string _text = string.Empty;
    private double _raw = double.NaN;

    public ComponentViewModel(string id)
    {
        Id = id;
    }

    /// <summary>Stable id, matches the owning <see cref="IWidgetComponent.Id"/>.</summary>
    public string Id { get; }

    public string Label
    {
        get { return _label; }
        private set { SetValue(ref _label, value); }
    }

    public string Text
    {
        get { return _text; }
        private set { SetValue(ref _text, value); }
    }

    /// <summary>Normalized magnitude in [0,1] when meaningful, else NaN (for coloring/gauges).</summary>
    public double Raw
    {
        get { return _raw; }
        private set { SetValue(ref _raw, value); }
    }

    /// <summary>Pushes a fresh snapshot into the bound properties. Call on the UI thread.</summary>
    public void Apply(ComponentValue value)
    {
        Label = value.Label;
        Text = value.Text;
        Raw = value.Raw;
    }
}
