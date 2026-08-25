using SysWidget.Services;

namespace SysWidget.Components;

/// <summary>
/// Current Windows virtual desktop, shown as "index/count" (e.g. "2/4"). A thin view over
/// <see cref="VirtualDesktopWatcher"/>: the watcher owns the registry reads and the notification
/// thread, so the layout keeps being tracked even when this component is toggled off. Push
/// component: it raises <see cref="WidgetComponentBase.SetValue"/> from the watcher thread, and
/// <see cref="ComponentHost"/> marshals that onto the UI thread.
/// </summary>
public sealed class VirtualDesktopComponent : WidgetComponentBase
{
    private Action<DesktopState>? _handler;

    public VirtualDesktopComponent() : base("vdesk", "Virtual desktop", ComponentKind.Text)
    {
    }

    public override void Start()
    {
        _handler = OnUpdated;
        VirtualDesktopWatcher.Updated += _handler;
        VirtualDesktopWatcher.Start();
    }

    public override void Sample()
    {
        Publish(VirtualDesktopWatcher.Current);
    }

    private void OnUpdated(DesktopState state)
    {
        Publish(state);
    }

    private void Publish(DesktopState state)
    {
        // SetValue dedups via record-struct equality, so redundant reads are cheap no-ops.
        SetValue(new ComponentValue("Desk", $"{state.Index}/{state.Count}", double.NaN));
    }

    public override void Dispose()
    {
        if (_handler is not null)
        {
            VirtualDesktopWatcher.Updated -= _handler;
            _handler = null;
        }

        base.Dispose();
    }
}
