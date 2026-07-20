using System.Runtime.InteropServices;

namespace SysWidget.Components;

/// <summary>
/// Physical memory in use, from <c>GlobalMemoryStatusEx</c>. Shows both the percentage
/// and the absolute amount used, e.g. "62% · 19,8G".
/// </summary>
public sealed class RamComponent : WidgetComponentBase
{
    public RamComponent() : base("ram", "Memory", ComponentKind.Gauge)
    {
    }

    public override void Sample()
    {
        MEMORYSTATUSEX status = new() { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return;
        }

        ulong total = status.ullTotalPhys;
        ulong used = total - status.ullAvailPhys;
        double fraction = total == 0 ? 0.0 : (double)used / total;
        int percent = (int)Math.Round(fraction * 100.0);

        SetValue(new ComponentValue("RAM", $"{percent}% · {FormatBytes(used)}", fraction));
    }

    private static string FormatBytes(ulong bytes)
    {
        double gb = bytes / (1024.0 * 1024.0 * 1024.0);
        if (gb >= 10.0)
        {
            return $"{gb:0}G";
        }

        return $"{gb:0.0}G";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
