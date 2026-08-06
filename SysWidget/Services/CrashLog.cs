using System.IO;
using System.Text;
using System.Windows.Threading;

namespace SysWidget.Services;

/// <summary>
/// Last-resort diagnostics: appends every unhandled exception to
/// <c>%LocalAppData%\SysWidget\crash.log</c>. Without this the widget dies silently — the Windows
/// event log only records the exception *code* (0xe0434352), never the managed stack, so a crash
/// leaves nothing to work from.
/// </summary>
public static class CrashLog
{
    /// <summary>Past this size the log is rolled over to <c>crash.log.old</c>.</summary>
    private const long MaxFileBytes = 1024 * 1024;

    private static readonly Lock Gate = new();

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysWidget");

    public static string FilePath { get; } = Path.Combine(Directory, "crash.log");

    /// <summary>
    /// Subscribes to the three channels an exception can escape through. Call this first thing
    /// in startup, before anything that can throw.
    /// </summary>
    public static void Install(System.Windows.Application app)
    {
        app.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static void Write(string source, Exception? exception)
    {
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                RollOverIfTooLarge();

                using StreamWriter writer = new(FilePath, append: true, Encoding.UTF8);
                writer.WriteLine($"===== {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}  [{source}]");
                // The build identity travels with every entry: a log kept across upgrades is
                // useless if it cannot say which binary produced which crash.
                writer.WriteLine($"      {AppVersion.Full}  ·  .NET {Environment.Version}  ·  Windows {Environment.OSVersion.Version}");
                writer.WriteLine(exception?.ToString() ?? "(no exception object)");
                writer.WriteLine();
            }
        }
        catch
        {
            // Logging a crash must never cause one.
        }
    }

    private static void RollOverIfTooLarge()
    {
        FileInfo info = new(FilePath);
        if (info.Exists && info.Length > MaxFileBytes)
        {
            File.Move(FilePath, FilePath + ".old", overwrite: true);
        }
    }

    /// <summary>
    /// Exceptions on the UI thread. These are logged and <em>swallowed</em>: the sources seen in
    /// practice are transient system-level failures around sleep/resume, and keeping a widget that
    /// has already rendered its state on screen beats vanishing without a trace. The log is the
    /// signal that something went wrong.
    /// </summary>
    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Write("Dispatcher", e.Exception);
        e.Handled = true;
    }

    /// <summary>
    /// Exceptions on any other thread (component watcher threads, the WPF render thread). The CLR
    /// tears the process down right after this returns — nothing to do but record why.
    /// </summary>
    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        string source = e.IsTerminating ? "AppDomain (terminating)" : "AppDomain";
        Write(source, e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Write("Task", e.Exception);
        e.SetObserved();
    }
}
