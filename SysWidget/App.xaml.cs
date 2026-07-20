using System.Windows;
using SysWidget.Components;
using SysWidget.Services;
using SysWidget.Settings;
using SysWidget.ViewModels;

namespace SysWidget;

public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = "SysWidget.SingleInstance.9f2c";

    private Mutex? _instanceMutex;
    private ComponentHost? _host;
    private TrayIcon? _tray;
    private WidgetWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance already runs; leave it be.
            Shutdown();
            return;
        }

        AppSettings settings = SettingsStore.Load();

        // Self-heal: if the user wanted startup on but the Run entry is missing or points at an
        // old path (e.g. the app folder was moved), re-register the current executable path.
        if (settings.StartWithWindows && !StartupManager.IsEnabled())
        {
            StartupManager.SetEnabled(true);
        }

        // Reconcile the persisted "start with Windows" intent with the actual registry state.
        settings.StartWithWindows = StartupManager.IsEnabled();

        SynchronizationContext sync = SynchronizationContext.Current
            ?? throw new InvalidOperationException("No synchronization context on the UI thread.");

        _host = new ComponentHost(sync);
        WidgetViewModel vm = new(settings, _host, Shutdown);

        _window = new WidgetWindow { DataContext = vm };
        _window.Show();

        _tray = new TrayIcon(vm);

        vm.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _host?.Dispose();
        _window?.Close();

        if (_instanceMutex is not null)
        {
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }

        base.OnExit(e);
    }
}
