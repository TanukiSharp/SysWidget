namespace SysWidget.Components;

/// <summary>
/// Convenience base handling <see cref="Value"/> storage and <see cref="ValueChanged"/>
/// plumbing. Poll components just override <see cref="Sample"/> and call
/// <see cref="SetValue"/>; push components call <see cref="SetValue"/> from their watcher.
/// </summary>
public abstract class WidgetComponentBase : IWidgetComponent
{
    private ComponentValue _value = ComponentValue.Empty;

    protected WidgetComponentBase(string id, string displayName, ComponentKind kind)
    {
        Id = id;
        DisplayName = displayName;
        Kind = kind;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public ComponentKind Kind { get; }

    public ComponentValue Value
    {
        get { return _value; }
    }

    public event Action? ValueChanged;

    public virtual void Start()
    {
    }

    public abstract void Sample();

    /// <summary>Updates <see cref="Value"/> and raises <see cref="ValueChanged"/> when it differs.</summary>
    protected void SetValue(ComponentValue value)
    {
        if (_value == value)
        {
            return;
        }

        _value = value;
        ValueChanged?.Invoke();
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
