using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net;

namespace ValCrown.Services;

/// <summary>
/// Network monitoring, optimization, and diagnostics.
/// </summary>
public static class NetworkService
{
    private static readonly string[] PingTargets = { "8.8.8.8", "1.1.1.1", "9.9.9.9" };

    // ── PING ──────────────────────────────────────────────────────────────────
    public static async Task<object> PingHost(string host)
    {
        try
        {
            using var ping  = new Ping();
            var reply = await ping.SendPingAsync(host, 3000);
            return new
            {
                ms      = reply.Status == IPStatus.Success ? reply.RoundtripTime : 999L,
                host,
                success = reply.Status == IPStatus.Success,
                status  = reply.Status.ToString()
            };
        }
        catch { return new { ms = 999L, host, success = false, status = "Error" }; }
    }

    // ── MULTI-PING (best of 3 servers) ────────────────────────────────────────
    public static async Task<object> GetBestPing()
    {
        var results = new List<(long ms, string host)>();
        foreach (var target in PingTargets)
        {
            try
            {
                using var ping  = new Ping();
                var reply = await ping.SendPingAsync(target, 2000);
                if (reply.Status == IPStatus.Success)
                    results.Add((reply.RoundtripTime, target));
            }
            catch { }
        }
        if (results.Count == 0) return new { ms = 999L, host = "N/A", quality = "No connection" };
        var best = results.MinBy(r => r.ms);
        var quality = best.ms < 30  ? "Excellent" :
                      best.ms < 60  ? "Good"      :
                      best.ms < 100 ? "Fair"      : "Poor";
        return new { ms = best.ms, host = best.host, quality };
    }

    // ── FLUSH DNS ─────────────────────────────────────────────────────────────
    public static object FlushDns()
    {
        try
        {
            RunCmd("ipconfig", "/flushdns");
            RunCmd("ipconfig", "/registerdns");
            RunCmd("netsh", "winsock reset catalog");
            return new { success = true, message = "DNS cache flushed + Winsock reset" };
        }
        catch (Exception ex) { return new { success = false, error = ex.Message }; }
    }

    // ── TCP OPTIMIZATION ──────────────────────────────────────────────────────
    public static object OptimizeTcp()
    {
        var applied = new List<string>();
        try
        {
            RunCmd("netsh", "int tcp set global autotuninglevel=normal");   applied.Add("AutoTuning: Normal");
            RunCmd("netsh", "int tcp set global rss=enabled");              applied.Add("RSS: Enabled");
            RunCmd("netsh", "int tcp set global chimney=disabled");         applied.Add("Chimney: Disabled");
            RunCmd("netsh", "int tcp set global dca=enabled");              applied.Add("DCA: Enabled");
            RunCmd("netsh", "int tcp set global ecncapability=enabled");    applied.Add("ECN: Enabled");
            // Disable Nagle for low latency gaming
            RunCmd("reg", @"add ""HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"" /v TcpAckFrequency /t REG_DWORD /d 1 /f");
            applied.Add("Nagle: Disabled (low latency)");
            RunCmd("reg", @"add ""HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"" /v TCPNoDelay /t REG_DWORD /d 1 /f");
            applied.Add("TCP NoDelay: Enabled");
            return new { success = true, applied };
        }
        catch (Exception ex) { return new { success = false, error = ex.Message, applied }; }
    }

    // ── NETWORK INFO ─────────────────────────────────────────────────────────
    public static object GetNetworkInfo()
    {
        var adapters = new List<object>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
            {
                var stats = nic.GetIPv4Statistics();
                adapters.Add(new
                {
                    name        = nic.Name,
                    type        = nic.NetworkInterfaceType.ToString(),
                    speedMbps   = nic.Speed / 1_000_000,
                    sentMb      = stats.BytesSent      / 1_048_576,
                    receivedMb  = stats.BytesReceived  / 1_048_576,
                    description = nic.Description
                });
            }
        }
        catch { }
        return new { adapters };
    }

    // ── DNS SERVERS ───────────────────────────────────────────────────────────
    public static object SetDns(string primary, string secondary)
    {
        try
        {
            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                     n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            if (adapter == null) return new { success = false, error = "No active adapter" };

            RunCmd("netsh", $"interface ip set dns \"{adapter.Name}\" static {primary}");
            RunCmd("netsh", $"interface ip add dns \"{adapter.Name}\" {secondary} index=2");
            return new { success = true, primary, secondary };
        }
        catch (Exception ex) { return new { success = false, error = ex.Message }; }
    }

    private static void RunCmd(string cmd, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(cmd, args)
            {
                CreateNoWindow = true, UseShellExecute = false
            });
            p?.WaitForExit(5000);
        }
        catch { }
    }
}
