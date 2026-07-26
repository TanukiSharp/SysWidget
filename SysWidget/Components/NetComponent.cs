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
    /// <summary>Beyond this gap between samples (host tick is 1 s) the rate is meaningless.</summary>
    private const double MaxIntervalSeconds = 10.0;

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

        long downDelta = received - _prevReceived;
        long upDelta = sent - _prevSent;

        _prevReceived = received;
        _prevSent = sent;

        // A counter going backwards means the set of adapters changed (one dropped out, or they
        // were recreated on resume); a long gap means the machine was asleep. Neither yields a
        // meaningful rate, so re-baseline on the current totals and show zero for this sample.
        if (downDelta < 0 || upDelta < 0 || elapsed <= 0.0 || elapsed > MaxIntervalSeconds)
        {
            SetValue(new ComponentValue("Net", "↓0 ↑0", double.NaN));
            return;
        }

        double downBps = downDelta / elapsed;
        double upBps = upDelta / elapsed;

        SetValue(new ComponentValue("Net", $"↓{FormatRate(downBps)} ↑{FormatRate(upBps)}", double.NaN));
    }

    private static (long Received, long Sent) ReadTotals()
    {
        long received = 0;
        long sent = 0;

        NetworkInterface[] nics;
        try
        {
            nics = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            // The whole stack can be unavailable for a moment (e.g. right after resume from sleep).
            return (received, sent);
        }

        foreach (NetworkInterface nic in nics)
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            try
            {
                IPInterfaceStatistics stats = nic.GetIPStatistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }
            catch (NetworkInformationException)
            {
                // The adapter vanished between enumeration and the stats read — adapters are torn
                // down and recreated on resume from sleep/hibernation. Skip it for this sample.
            }
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
