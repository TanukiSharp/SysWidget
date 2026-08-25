using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SysWidget.Services;

/// <summary>Snapshot of the virtual desktop layout: 1-based index, total count, and the current desktop's GUID.</summary>
public readonly record struct DesktopState(int Index, int Count, Guid Id);

/// <summary>A move between two desktops. <paramref name="Count"/> is the total at the time of the move.</summary>
public readonly record struct DesktopSwitch(int From, int To, int Count);

/// <summary>
/// Single source of truth for the current Windows virtual desktop. Reads the layout from the
/// registry — the same source Explorer uses — and updates instantly via
/// <c>RegNotifyChangeKeyValue</c> on a background thread.
/// Static because it outlives the optional "vdesk" widget component: the switch overlay needs it
/// whether or not the component is shown. Events are raised from the watcher thread; subscribers
/// marshal onto their own thread.
/// Registry-reading logic adapted from the standalone VirtualDesktopNumberIndicator app.
/// </summary>
public static class VirtualDesktopWatcher
{
    // HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops -> VirtualDesktopIDs (ordered GUID blob)
    private const string VirtualDesktopsSubKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

    // HKCU\...\Explorer\SessionInfo\<sessionId>\VirtualDesktops -> CurrentVirtualDesktop (GUID)
    private static string SessionSubKey
    {
        get
        {
            return $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\{Process.GetCurrentProcess().SessionId}\VirtualDesktops";
        }
    }

    private static readonly object Gate = new();

    private static Thread? _notifyThread;
    private static volatile bool _stopped;
    private static bool _hasState;
    private static DesktopState _current = new(1, 1, Guid.Empty);

    /// <summary>Last known layout. Never throws; falls back to desktop 1 of 1 before the first read.</summary>
    public static DesktopState Current
    {
        get
        {
            lock (Gate)
            {
                return _current;
            }
        }
    }

    /// <summary>Raised whenever the layout changes at all — index, count, or identity.</summary>
    public static event Action<DesktopState>? Updated;

    /// <summary>
    /// Raised only for a genuine move between desktops. Deliberately silent when the count changed
    /// at the same time: adding or removing a desktop shifts indices without the user going
    /// anywhere, and reporting that as a switch would be a lie.
    /// </summary>
    public static event Action<DesktopSwitch>? Switched;

    /// <summary>Starts the watcher thread. Idempotent.</summary>
    public static void Start()
    {
        lock (Gate)
        {
            if (_notifyThread is not null)
            {
                return;
            }

            _stopped = false;
            _notifyThread = new Thread(NotifyLoop) { IsBackground = true, Name = "VirtualDesktopWatcher" };
            _notifyThread.Start();
        }
    }

    /// <summary>
    /// Signals the watcher thread to exit. It is a background thread and is never joined, so a
    /// blocked wait can outlive this call by up to the 2 s backstop.
    /// </summary>
    public static void Stop()
    {
        _stopped = true;
    }

    /// <summary>Re-reads the registry and raises the events if anything moved. Safe to call from any thread.</summary>
    public static void Refresh()
    {
        DesktopState state = Read();

        DesktopState previous;
        bool hadState;
        lock (Gate)
        {
            previous = _current;
            hadState = _hasState;
            if (hadState && previous == state)
            {
                return;
            }

            _current = state;
            _hasState = true;
        }

        Updated?.Invoke(state);

        // No "from" on the very first sample, and no switch when the layout itself was reshaped.
        if (hadState && previous.Index != state.Index && previous.Count == state.Count)
        {
            Switched?.Invoke(new DesktopSwitch(previous.Index, state.Index, state.Count));
        }
    }

    // --- Registry reads (layout varies by Windows build) ---

    private static DesktopState Read()
    {
        try
        {
            Guid current = ReadCurrentDesktopId();
            Guid[] order = ReadDesktopOrder();
            int count = order.Length;

            int index = 1;
            if (current != Guid.Empty && count > 0)
            {
                int found = Array.IndexOf(order, current);
                if (found >= 0)
                {
                    index = found + 1;
                }
            }

            if (count == 0)
            {
                count = 1;
            }

            return new DesktopState(index, count, current);
        }
        catch
        {
            // Registry layout can vary; fall back to a sane default on transient errors.
            return new DesktopState(1, 1, Guid.Empty);
        }
    }

    private static Guid ReadCurrentDesktopId()
    {
        // Newer builds (25H2/26200) store CurrentVirtualDesktop under the main VirtualDesktops
        // key, older ones under the per-session key. Try session first, then fall back.
        using (RegistryKey? sessionKey = Registry.CurrentUser.OpenSubKey(SessionSubKey))
        {
            if (sessionKey?.GetValue("CurrentVirtualDesktop") is byte[] { Length: 16 } sb)
            {
                return new Guid(sb);
            }
        }

        using (RegistryKey? mainKey = Registry.CurrentUser.OpenSubKey(VirtualDesktopsSubKey))
        {
            if (mainKey?.GetValue("CurrentVirtualDesktop") is byte[] { Length: 16 } mb)
            {
                return new Guid(mb);
            }
        }

        return Guid.Empty;
    }

    private static Guid[] ReadDesktopOrder()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(VirtualDesktopsSubKey);
        if (key?.GetValue("VirtualDesktopIDs") is byte[] blob && blob.Length >= 16)
        {
            int n = blob.Length / 16;
            Guid[] guids = new Guid[n];
            for (int i = 0; i < n; i++)
            {
                guids[i] = new Guid(blob.AsSpan(i * 16, 16));
            }

            return guids;
        }

        return [];
    }

    // --- RegNotifyChangeKeyValue based instant notifications ---

    private static void NotifyLoop()
    {
        // Publish an initial state so Current is meaningful before the first change.
        try
        {
            Refresh();
        }
        catch
        {
        }

        while (!_stopped)
        {
            try
            {
                if (!WaitForChange())
                {
                    // Keys not present yet; back off briefly and retry.
                    Thread.Sleep(500);
                    continue;
                }

                if (_stopped)
                {
                    break;
                }

                Refresh();
            }
            catch
            {
                Thread.Sleep(500);
            }
        }
    }

    /// <summary>Blocks until either watched key changes. Returns false if neither can be opened.</summary>
    private static bool WaitForChange()
    {
        using ManualResetEvent sessionEvent = new(false);
        using ManualResetEvent listEvent = new(false);

        // Re-opened every iteration on purpose: the session key is torn down and rebuilt across
        // suspend/resume, and a stale handle would silently stop delivering notifications.
        SafeRegistryHandle? sessionKey = OpenKey(SessionSubKey);
        SafeRegistryHandle? listKey = OpenKey(VirtualDesktopsSubKey);

        try
        {
            bool any = false;
            if (sessionKey is { IsInvalid: false })
            {
                RegNotifyChangeKeyValue(sessionKey.DangerousGetHandle(), true,
                    RegNotifyFilter.LastSet, sessionEvent.SafeWaitHandle.DangerousGetHandle(), true);
                any = true;
            }

            if (listKey is { IsInvalid: false })
            {
                RegNotifyChangeKeyValue(listKey.DangerousGetHandle(), true,
                    RegNotifyFilter.LastSet | RegNotifyFilter.Name, listEvent.SafeWaitHandle.DangerousGetHandle(), true);
                any = true;
            }

            if (!any)
            {
                return false;
            }

            WaitHandle[] handles = [sessionEvent, listEvent];
            WaitHandle.WaitAny(handles, 2000); // wake at least every 2s as a backstop
            return true;
        }
        finally
        {
            sessionKey?.Dispose();
            listKey?.Dispose();
        }
    }

    private static SafeRegistryHandle? OpenKey(string subKey)
    {
        int result = RegOpenKeyEx(HKEY_CURRENT_USER, subKey, 0, KEY_NOTIFY, out IntPtr handle);
        if (result != 0)
        {
            return null;
        }

        return new SafeRegistryHandle(handle, ownsHandle: true);
    }

    // --- P/Invoke ---

    private const int HKEY_CURRENT_USER = unchecked((int)0x80000001);
    private const int KEY_NOTIFY = 0x0010;

    [Flags]
    private enum RegNotifyFilter
    {
        Name = 0x00000001,
        Attributes = 0x00000002,
        LastSet = 0x00000004,
        Security = 0x00000008,
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegOpenKeyEx(int hKey, string subKey, int options, int samDesired, out IntPtr result);

    [DllImport("advapi32.dll")]
    private static extern int RegNotifyChangeKeyValue(
        IntPtr hKey, bool watchSubtree, RegNotifyFilter notifyFilter, IntPtr hEvent, bool asynchronous);

    private sealed class SafeRegistryHandle : SafeHandle
    {
        public SafeRegistryHandle(IntPtr handle, bool ownsHandle) : base(IntPtr.Zero, ownsHandle)
        {
            SetHandle(handle);
        }

        public override bool IsInvalid
        {
            get { return handle == IntPtr.Zero; }
        }

        protected override bool ReleaseHandle()
        {
            return RegCloseKey(handle) == 0;
        }

        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr hKey);
    }
}
