namespace SysWidget.Components;

/// <summary>Metadata + factory for one kind of component available to the widget.</summary>
/// <param name="Id">Stable id persisted in settings.</param>
/// <param name="DisplayName">Name shown in the tray menu.</param>
/// <param name="Create">Factory for a fresh instance.</param>
public sealed record ComponentRegistration(string Id, string DisplayName, Func<IWidgetComponent> Create);

/// <summary>
/// The set of components the widget knows how to show, in default display order.
/// Adding a new component = one entry here plus its <see cref="IWidgetComponent"/> class.
/// </summary>
public static class ComponentCatalog
{
    public static IReadOnlyList<ComponentRegistration> All { get; } =
    [
        new("vdesk", "Virtual desktop", static () => new VirtualDesktopComponent()),
        new("cpu", "CPU", static () => new CpuComponent()),
        new("ram", "Memory", static () => new RamComponent()),
        new("net", "Network", static () => new NetComponent()),
    ];

    public static ComponentRegistration? Find(string id)
    {
        foreach (ComponentRegistration reg in All)
        {
            if (string.Equals(reg.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return reg;
            }
        }

        return null;
    }
}
