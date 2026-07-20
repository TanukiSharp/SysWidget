namespace SysWidget.Components;

/// <summary>
/// Immutable snapshot produced by a component on each sample. Allocation-free to build
/// (a struct); <see cref="Text"/> is the only heap allocation and only when it changes.
/// </summary>
/// <param name="Label">Short caption, e.g. "CPU".</param>
/// <param name="Text">Formatted value for display, e.g. "12%".</param>
/// <param name="Raw">Normalized magnitude in [0,1] when meaningful (for gauges/coloring); NaN otherwise.</param>
public readonly record struct ComponentValue(string Label, string Text, double Raw)
{
    public static ComponentValue Empty { get; } = new(string.Empty, string.Empty, double.NaN);
}
