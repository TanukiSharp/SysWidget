namespace SysWidget.Components;

/// <summary>How a component wants to be rendered; selects its view model and DataTemplate.</summary>
public enum ComponentKind
{
    /// <summary>Label + value text only (e.g. network throughput).</summary>
    Text,

    /// <summary>A vertical green-to-red bar driven by <see cref="ComponentValue.Raw"/>, plus the value text.</summary>
    Gauge,
}

/// <summary>
/// A single monitored quantity shown in the widget (CPU, RAM, network, and later
/// virtual-desktop number, etc.). Two update models are supported:
/// <list type="bullet">
/// <item><b>poll</b> — the host calls <see cref="Sample"/> on a fixed cadence (CPU/RAM/net).</item>
/// <item><b>push</b> — the component raises <see cref="ValueChanged"/> when it detects a
/// change on its own (e.g. a registry watcher); the poll tick is then just a safety net.</item>
/// </list>
/// A component may use either or both. Adding a new component is one class here plus one
/// line in <see cref="ComponentCatalog"/>.
/// </summary>
public interface IWidgetComponent : IDisposable
{
    /// <summary>Stable identifier persisted in settings (e.g. "cpu").</summary>
    string Id { get; }

    /// <summary>Human-readable name shown in the tray menu (e.g. "CPU").</summary>
    string DisplayName { get; }

    /// <summary>How this component should be rendered.</summary>
    ComponentKind Kind { get; }

    /// <summary>Latest snapshot. Read after <see cref="Sample"/> or on <see cref="ValueChanged"/>.</summary>
    ComponentValue Value { get; }

    /// <summary>Raised (on any thread) by push components when <see cref="Value"/> has changed.</summary>
    event Action? ValueChanged;

    /// <summary>Called once when the component becomes active. Push components start watching here.</summary>
    void Start();

    /// <summary>Refresh <see cref="Value"/> from the source. Called on the host tick for poll components.</summary>
    void Sample();
}
