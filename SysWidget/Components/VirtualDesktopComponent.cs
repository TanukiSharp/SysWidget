using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SysWidget.Components;

/// <summary>
/// Current Windows virtual desktop, shown as "index/count" (e.g. "2/4"). Reads the desktop
/// layout from the registry — the same source Explorer uses — and updates instantly via
/// <c>RegNotifyChangeKeyValue</c> on a background thread, with the host poll tick as a safety
/// net. Push component: it raises <see cref="WidgetComponentBase.SetValue"/> from its watcher,
/// and <see cref="ComponentHost"/> marshals that onto the UI thread.
/// Registry-reading logic adapted from the standalone VirtualDesktopNumberIndicator app.
/// </summary>
public sealed class VirtualDesktopComponent : WidgetComponentBase
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

    private Thread? _notifyThread;
    private volatile bool _disposed;

    public VirtualDesktopComponent() : base("vdesk", "Virtual desktop", ComponentKind.Text)
    {
    }

    public override void Start()
    {
        // Instant updates: block on a registry-change notification, re-read, repeat.
        _notifyThread = new Thread(NotifyLoop) { IsBackground = true, Name = "VirtualDesktopWatcher" };
        _notifyThread.Start();
    }

    public override void Sample()
    {
        Publish();
    }

    private void Publish()
    {
        (int index, int count) = Read();
        // SetValue dedups via record-struct equality, so redundant reads are cheap no-ops.
        SetValue(new ComponentValue("Desk", $"{index}/{count}", double.NaN));
    }

    // --- Registry reads (layout varies by Windows build) ---

    private static (int Index, int Count) Read()
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

            return (index, count);
        }
        catch
        {
            // Registry layout can vary; fall back to a sane default on transient errors.
            return (1, 1);
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

    private void NotifyLoop()
    {
        while (!_disposed)
        {
            try
            {
                if (!WaitForChange())
                {
                    // Keys not present yet; back off briefly and retry.
                    Thread.Sleep(500);
                    continue;
                }

                if (_disposed)
                {
                    break;
                }

                Publish();
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

    public override void Dispose()
    {
        _disposed = true;
        base.Dispose();
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
