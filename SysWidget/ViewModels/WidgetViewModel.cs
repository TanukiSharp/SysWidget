using System.Collections.ObjectModel;
using System.Windows.Input;
using SysWidget.Components;
using SysWidget.Services;
using SysWidget.Settings;

namespace SysWidget.ViewModels;

/// <summary>
/// Root view model bound to the widget window. Owns the mutable <see cref="AppSettings"/>
/// and the <see cref="ComponentHost"/>; the view binds to it exclusively (no code-behind
/// references either way).
/// </summary>
public sealed class WidgetViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly ComponentHost _host;
    private readonly Action _shutdown;
    private int _sizeResetToken;
    private int _positionResetToken;

    public WidgetViewModel(AppSettings settings, ComponentHost host, Action shutdown)
    {
        _settings = settings;
        _host = host;
        _shutdown = shutdown;

        _host.Configure(settings.ActiveComponents);

        List<ComponentToggleViewModel> toggles = [];
        foreach (ComponentRegistration reg in ComponentCatalog.All)
        {
            bool active = settings.ActiveComponents.Contains(reg.Id, StringComparer.OrdinalIgnoreCase);
            toggles.Add(new ComponentToggleViewModel(reg.Id, reg.DisplayName, active, OnComponentToggled));
        }

        AvailableComponents = toggles;

        ToggleThemeCommand = new RelayCommand(() => Theme = IsDark ? WidgetTheme.Light : WidgetTheme.Dark);
        ResetSizeCommand = new RelayCommand(() => SizeResetToken++);
        ResetPositionCommand = new RelayCommand(() => PositionResetToken++);
        QuitCommand = new RelayCommand(() => _shutdown());
    }

    /// <summary>Active components in order — the ItemsControl source.</summary>
    public ObservableCollection<ComponentViewModel> Components
    {
        get { return _host.Views; }
    }

    /// <summary>All catalog components with a bindable active flag (for the menu).</summary>
    public IReadOnlyList<ComponentToggleViewModel> AvailableComponents { get; }

    public double Opacity
    {
        get { return _settings.Opacity; }
        set
        {
            double clamped = Math.Clamp(value, 0.2, 1.0);
            if (Math.Abs(_settings.Opacity - clamped) < 0.001)
            {
                return;
            }

            _settings.Opacity = clamped;
            RaisePropertyChanged();
            Save();
        }
    }

    public WidgetTheme Theme
    {
        get { return _settings.Theme; }
        set
        {
            if (_settings.Theme == value)
            {
                return;
            }

            _settings.Theme = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsDark));
            Save();
        }
    }

    /// <summary>Convenience flag for XAML triggers.</summary>
    public bool IsDark
    {
        get { return Theme == WidgetTheme.Dark; }
    }

    /// <summary>
    /// Bumped by <see cref="ResetSizeCommand"/>; the grow-only width behavior clears its ratchet
    /// whenever this changes, so the widget can shrink back to fit its current content.
    /// </summary>
    public int SizeResetToken
    {
        get { return _sizeResetToken; }
        private set { SetValue(ref _sizeResetToken, value); }
    }

    /// <summary>
    /// Bumped by <see cref="ResetPositionCommand"/>; the placement behavior snaps the window
    /// back to its default top-right spot whenever this changes — recovery for when another app
    /// (e.g. Magnifier) relocates or buries the widget.
    /// </summary>
    public int PositionResetToken
    {
        get { return _positionResetToken; }
        private set { SetValue(ref _positionResetToken, value); }
    }

    public double? WindowLeft
    {
        get { return _settings.WindowLeft; }
        set
        {
            if (Nullable.Equals(_settings.WindowLeft, value))
            {
                return;
            }

            _settings.WindowLeft = value;
            RaisePropertyChanged();
            Save();
        }
    }

    public double? WindowTop
    {
        get { return _settings.WindowTop; }
        set
        {
            if (Nullable.Equals(_settings.WindowTop, value))
            {
                return;
            }

            _settings.WindowTop = value;
            RaisePropertyChanged();
            Save();
        }
    }

    public bool StartWithWindows
    {
        get { return _settings.StartWithWindows; }
        set
        {
            if (_settings.StartWithWindows == value)
            {
                return;
            }

            _settings.StartWithWindows = value;
            StartupManager.SetEnabled(value);
            RaisePropertyChanged();
            Save();
        }
    }

    public ICommand ToggleThemeCommand { get; }

    public ICommand ResetSizeCommand { get; }

    public ICommand ResetPositionCommand { get; }

    public ICommand QuitCommand { get; }

    public void Start()
    {
        _host.Start();
    }

    private void Save()
    {
        SettingsStore.Save(_settings);
    }

    private void OnComponentToggled(ComponentToggleViewModel _)
    {
        List<string> active = [];
        foreach (ComponentToggleViewModel toggle in AvailableComponents)
        {
            if (toggle.IsActive)
            {
                active.Add(toggle.Id);
            }
        }

        if (active.Count == 0)
        {
            // Never allow an empty widget; keep at least CPU.
            foreach (ComponentToggleViewModel toggle in AvailableComponents)
            {
                if (string.Equals(toggle.Id, "cpu", StringComparison.OrdinalIgnoreCase) && !toggle.IsActive)
                {
                    toggle.IsActive = true; // setter re-enters OnComponentToggled; let that pass rebuild.
                    return;
                }
            }
        }

        _settings.ActiveComponents = active;
        _host.Configure(active);
        Save();
    }
}
