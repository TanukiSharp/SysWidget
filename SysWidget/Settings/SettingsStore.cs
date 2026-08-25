using System.IO;
using System.Text.Json;

namespace SysWidget.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON under %AppData%\SysWidget.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SysWidget");

    public static string FilePath { get; } = Path.Combine(Directory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded is not null)
                {
                    Sanitize(loaded);
                    return loaded;
                }
            }
        }
        catch
        {
            // Corrupt or unreadable settings: fall back to defaults rather than crash.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            string json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Non-fatal: a failed save must not take down the widget.
        }
    }

    private static void Sanitize(AppSettings s)
    {
        s.Opacity = Math.Clamp(s.Opacity, 0.2, 1.0);
        s.DesktopSwitchSizePercent = Math.Clamp(s.DesktopSwitchSizePercent, 0.05, 0.90);
        s.DesktopSwitchHoldSeconds = Math.Clamp(s.DesktopSwitchHoldSeconds, 0.0, 10.0);
        s.DesktopSwitchFadeSeconds = Math.Clamp(s.DesktopSwitchFadeSeconds, 0.05, 10.0);
        s.ActiveComponents ??= [];
        s.ActiveComponents.RemoveAll(id => Components.ComponentCatalog.Find(id) is null);
        if (s.ActiveComponents.Count == 0)
        {
            s.ActiveComponents = ["cpu", "ram", "net"];
        }
    }
}
