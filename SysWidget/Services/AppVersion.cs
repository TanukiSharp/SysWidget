using System.Reflection;

namespace SysWidget.Services;

/// <summary>
/// Identity of the running build: the SemVer from the project file plus the git commit it was
/// built from. Both are baked into <see cref="AssemblyInformationalVersionAttribute"/> by the
/// ResolveGitCommit target in SysWidget.csproj, in the form <c>0.1.0+a1b2c3d4</c>.
/// </summary>
public static class AppVersion
{
    /// <summary>Semantic version, e.g. <c>0.1.0</c>.</summary>
    public static string SemVer { get; }

    /// <summary>Short commit hash, e.g. <c>a1b2c3d4</c> — <c>unknown</c> for a non-git build.</summary>
    public static string Commit { get; }

    /// <summary>Version and commit together, e.g. <c>0.1.0 (a1b2c3d4)</c>.</summary>
    public static string Display { get; }

    /// <summary>Product name included, e.g. <c>SysWidget 0.1.0 (a1b2c3d4)</c>.</summary>
    public static string Full { get; }

    static AppVersion()
    {
        string informational = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

        int plus = informational.IndexOf('+');
        string semVer = plus >= 0 ? informational[..plus] : informational;
        SemVer = semVer.Length > 0 ? semVer : "0.0.0";
        Commit = plus >= 0 && plus + 1 < informational.Length ? informational[(plus + 1)..] : "unknown";

        Display = $"{SemVer} ({Commit})";
        Full = $"SysWidget {Display}";
    }
}
