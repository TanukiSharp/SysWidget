namespace SysWidget.Settings;

public enum WidgetTheme
{
    Dark,
    Light,
}

/// <summary>
/// User-persisted state. Plain mutable POCO for straightforward JSON round-tripping;
/// wrapped by <see cref="SysWidget.ViewModels.WidgetViewModel"/> for binding.
/// </summary>
public sealed class AppSettings
{
    public WidgetTheme Theme { get; set; } = WidgetTheme.Dark;

    /// <summary>Opacity of the widget background in [0.2, 1].</summary>
    public double Opacity { get; set; } = 0.85;

    /// <summary>Ordered ids of the components to show. Defaults to virtual desktop, CPU, RAM, network.</summary>
    public List<string> ActiveComponents { get; set; } = ["vdesk", "cpu", "ram", "net"];

    /// <summary>Last window position (device-independent pixels); null until first placed.</summary>
    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public bool StartWithWindows { get; set; }

    /// <summary>Show the full-screen "1 → 2" overlay when Windows switches virtual desktop.</summary>
    public bool ShowDesktopSwitch { get; set; } = true;

    /// <summary>Overlay text height as a fraction of min(monitor width, monitor height), in [0.05, 0.9].</summary>
    public double DesktopSwitchSizePercent { get; set; } = 0.30;

    /// <summary>Seconds the overlay stays fully visible before fading, in [0, 10].</summary>
    public double DesktopSwitchHoldSeconds { get; set; } = 1.0;

    /// <summary>Seconds the fade-out lasts, in [0.05, 10].</summary>
    public double DesktopSwitchFadeSeconds { get; set; } = 1.0;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Theme = Theme,
            Opacity = Opacity,
            ActiveComponents = [.. ActiveComponents],
            WindowLeft = WindowLeft,
            WindowTop = WindowTop,
            StartWithWindows = StartWithWindows,
            ShowDesktopSwitch = ShowDesktopSwitch,
            DesktopSwitchSizePercent = DesktopSwitchSizePercent,
            DesktopSwitchHoldSeconds = DesktopSwitchHoldSeconds,
            DesktopSwitchFadeSeconds = DesktopSwitchFadeSeconds,
        };
    }
}
