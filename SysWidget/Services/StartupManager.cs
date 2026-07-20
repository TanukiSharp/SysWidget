using Microsoft.Win32;

namespace SysWidget.Services;

/// <summary>Manages the "start with Windows" HKCU Run entry.</summary>
public static class StartupManager
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SysWidget";

    /// <summary>
    /// True only when the Run entry exists <em>and</em> points at the current executable.
    /// A stale entry (e.g. after moving the app) is treated as not-enabled so callers can heal it.
    /// </summary>
    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
        if (key?.GetValue(ValueName) is not string stored)
        {
            return false;
        }

        string? current = Environment.ProcessPath;
        return current is not null
            && string.Equals(Unquote(stored), current, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve executable path.");
            key.SetValue(ValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>Strips a single pair of surrounding double quotes, if present.</summary>
    private static string Unquote(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }
}
