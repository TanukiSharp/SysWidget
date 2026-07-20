using System.Runtime.InteropServices;

namespace SysWidget.Components;

/// <summary>
/// Total CPU load, computed from the delta of <c>GetSystemTimes</c> between samples
/// (idle / kernel / user). Same basis as the legacy Task Manager reading: accurate,
/// locale-independent, and allocation-free (no PerformanceCounter).
/// </summary>
public sealed class CpuComponent : WidgetComponentBase
{
    private ulong _prevIdle;
    private ulong _prevKernel;
    private ulong _prevUser;
    private bool _hasPrev;

    public CpuComponent() : base("cpu", "CPU", ComponentKind.Gauge)
    {
    }

    public override void Sample()
    {
        if (!GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user))
        {
            return;
        }

        ulong idleT = ToUInt64(idle);
        ulong kernelT = ToUInt64(kernel);
        ulong userT = ToUInt64(user);

        if (!_hasPrev)
        {
            _prevIdle = idleT;
            _prevKernel = kernelT;
            _prevUser = userT;
            _hasPrev = true;
            return;
        }

        // kernel already includes idle, so total = kernel + user, busy = total - idle.
        ulong idleDelta = idleT - _prevIdle;
        ulong totalDelta = (kernelT - _prevKernel) + (userT - _prevUser);

        _prevIdle = idleT;
        _prevKernel = kernelT;
        _prevUser = userT;

        double usage = totalDelta == 0 ? 0.0 : 1.0 - ((double)idleDelta / totalDelta);
        if (usage < 0.0)
        {
            usage = 0.0;
        }
        else if (usage > 1.0)
        {
            usage = 1.0;
        }

        int percent = (int)Math.Round(usage * 100.0);
        SetValue(new ComponentValue("CPU", $"{percent}%", usage));
    }

    private static ulong ToUInt64(in FILETIME ft)
    {
        return ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public int dwLowDateTime;
        public int dwHighDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);
}
