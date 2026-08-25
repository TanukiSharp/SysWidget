using System.Runtime.InteropServices;

namespace SysWidget.Interop;

/// <summary>
/// Minimal binding to the documented shell <c>IVirtualDesktopManager</c>. Used purely as a safety
/// net by the switch overlay: a window is bound to the desktop that was current when it was
/// created, and the registry notification can land a hair before Windows finishes the switch, so
/// an overlay may be born on the desktop we just left. Everything here is best-effort — the
/// interface is only present on Windows 10+, and both calls can legitimately fail.
/// </summary>
internal static class VirtualDesktopManager
{
    private static readonly Guid ClsidVirtualDesktopManager = new("AA509086-5CA9-4C25-8F95-589D3C07B48A");

    private static IVirtualDesktopManager? _instance;
    private static bool _unavailable;

    /// <summary>
    /// Makes sure <paramref name="hwnd"/> sits on <paramref name="desktopId"/>, moving it if not.
    /// Silently does nothing when the shell interface is unavailable.
    /// </summary>
    public static void EnsureOnDesktop(IntPtr hwnd, Guid desktopId)
    {
        if (desktopId == Guid.Empty)
        {
            return;
        }

        IVirtualDesktopManager? manager = GetInstance();
        if (manager is null)
        {
            return;
        }

        try
        {
            if (manager.IsWindowOnCurrentVirtualDesktop(hwnd))
            {
                return;
            }

            manager.MoveWindowToDesktop(hwnd, ref desktopId);
        }
        catch (COMException)
        {
            // The window may not be eligible (not yet realised, wrong style); leave it where it is.
        }
    }

    private static IVirtualDesktopManager? GetInstance()
    {
        if (_unavailable)
        {
            return null;
        }

        if (_instance is not null)
        {
            return _instance;
        }

        try
        {
            Type? type = Type.GetTypeFromCLSID(ClsidVirtualDesktopManager);
            if (type is not null && Activator.CreateInstance(type) is IVirtualDesktopManager created)
            {
                _instance = created;
                return _instance;
            }
        }
        catch (Exception)
        {
        }

        _unavailable = true;
        return null;
    }

    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);

        Guid GetWindowDesktopId(IntPtr topLevelWindow);

        void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }
}
