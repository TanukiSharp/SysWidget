using System.Diagnostics;
using System.Net.NetworkInformation;

namespace SysWidget.Components;

/// <summary>
/// Aggregate network throughput (download / upload) across all operational, non-loopback,
/// non-tunnel interfaces. Uses byte counters from <see cref="IPInterfaceStatistics"/> and
/// divides by the measured elapsed time so the rate stays correct despite tick jitter.
/// </summary>
public sealed class NetComponent : WidgetComponentBase
{
    private readonly Stopwatch _clock = new();
    private long _prevReceived;
    private long _prevSent;
    private bool _hasPrev;

    public NetComponent() : base("net", "Network", ComponentKind.Text)
    {
    }

    public override void Sample()
    {
        (long received, long sent) = ReadTotals();

        double elapsed = _hasPrev ? _clock.Elapsed.TotalSeconds : 0.0;
        _clock.Restart();

        if (!_hasPrev)
        {
            _prevReceived = received;
            _prevSent = sent;
            _hasPrev = true;
            // Emit a zero baseline immediately so the widget starts at its full width.
            SetValue(new ComponentValue("Net", "↓0 ↑0", double.NaN));
            return;
        }

        double downBps = elapsed > 0 ? Math.Max(0, received - _prevReceived) / elapsed : 0.0;
        double upBps = elapsed > 0 ? Math.Max(0, sent - _prevSent) / elapsed : 0.0;

        _prevReceived = received;
        _prevSent = sent;

        SetValue(new ComponentValue("Net", $"↓{FormatRate(downBps)} ↑{FormatRate(upBps)}", double.NaN));
    }

    private static (long Received, long Sent) ReadTotals()
    {
        long received = 0;
        long sent = 0;

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            IPInterfaceStatistics stats = nic.GetIPStatistics();
            received += stats.BytesReceived;
            sent += stats.BytesSent;
        }

        return (received, sent);
    }

    /// <summary>Formats bytes/second as a compact rate, e.g. "3,1M", "812K", "0".</summary>
    private static string FormatRate(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024.0)
        {
            return "0";
        }

        double kb = bytesPerSecond / 1024.0;
        if (kb < 1024.0)
        {
            return $"{kb:0}K";
        }

        double mb = kb / 1024.0;
        if (mb < 1024.0)
        {
            return mb >= 10.0 ? $"{mb:0}M" : $"{mb:0.0}M";
        }

        double gb = mb / 1024.0;
        return gb >= 10.0 ? $"{gb:0}G" : $"{gb:0.0}G";
    }
}
