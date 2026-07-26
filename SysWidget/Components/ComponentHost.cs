using System.Collections.ObjectModel;
using System.Windows.Threading;
using SysWidget.ViewModels;

namespace SysWidget.Components;

/// <summary>
/// Owns the active components and their <see cref="ComponentViewModel"/>s, drives the
/// 1 s poll tick, and marshals both poll and push updates onto the UI thread. This is a
/// service (created on the UI thread), not a view model; the view binds to
/// <see cref="Views"/> only.
/// </summary>
public sealed class ComponentHost : IDisposable
{
    private readonly SynchronizationContext _sync;
    private readonly DispatcherTimer _timer;
    private readonly List<Entry> _entries = [];

    public ComponentHost(SynchronizationContext sync)
    {
        _sync = sync;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>Active components in display order — the view's ItemsSource.</summary>
    public ObservableCollection<ComponentViewModel> Views { get; } = [];

    /// <summary>
    /// Rebuilds the active set from an ordered list of component ids. Unknown ids are
    /// skipped. Safe to call at runtime when the user toggles components.
    /// </summary>
    public void Configure(IEnumerable<string> orderedActiveIds)
    {
        Teardown();

        foreach (string id in orderedActiveIds)
        {
            ComponentRegistration? reg = ComponentCatalog.Find(id);
            if (reg is null)
            {
                continue;
            }

            IWidgetComponent component = reg.Create();
            ComponentViewModel vm = component.Kind == ComponentKind.Gauge
                ? new GaugeComponentViewModel(component.Id)
                : new ComponentViewModel(component.Id);
            Action handler = () => OnValueChanged(component, vm);

            component.ValueChanged += handler;
            _entries.Add(new Entry(component, vm, handler));
            Views.Add(vm);
        }

        foreach (Entry entry in _entries)
        {
            entry.Component.Start();
            SafeSample(entry.Component);
            entry.ViewModel.Apply(entry.Component.Value);
        }
    }

    public void Start()
    {
        _timer.Start();
    }

    private void Tick()
    {
        // Runs on the UI thread; Sample() may fire ValueChanged synchronously, which the
        // handler marshals through _sync (a no-op hop when already on the UI thread).
        foreach (Entry entry in _entries)
        {
            SafeSample(entry.Component);
        }
    }

    /// <summary>
    /// Samples a component, swallowing failures. System sensors go transiently unavailable —
    /// most notably around sleep/hibernation, where adapters and WMI providers are torn down and
    /// recreated. This runs on the UI thread from the timer tick, so an escaping exception would
    /// take the whole widget down; skipping one sample keeps the previous value on screen instead.
    /// </summary>
    private static void SafeSample(IWidgetComponent component)
    {
        try
        {
            component.Sample();
        }
        catch (Exception)
        {
        }
    }

    private void OnValueChanged(IWidgetComponent component, ComponentViewModel vm)
    {
        _sync.Post(_ => vm.Apply(component.Value), null);
    }

    private void Teardown()
    {
        foreach (Entry entry in _entries)
        {
            entry.Component.ValueChanged -= entry.Handler;
            entry.Component.Dispose();
        }

        _entries.Clear();
        Views.Clear();
    }

    public void Dispose()
    {
        _timer.Stop();
        Teardown();
    }

    private sealed record Entry(IWidgetComponent Component, ComponentViewModel ViewModel, Action Handler);
}
