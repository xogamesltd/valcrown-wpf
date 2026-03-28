using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;

// Explicit alias to avoid ambiguity with System.Diagnostics.PerformanceCounter
using WinPerf = System.Diagnostics.PerformanceCounter;

namespace ValCrown.Services;

/// <summary>
/// Handles all JS → C# bridge calls from the WebView2.
/// Every method is wrapped in try/catch — the app never crashes due to bridge errors.
/// </summary>
public static class BridgeService
{
    public static async Task<object?> Handle(string action, JsonElement payload)
    {
        return action switch
        {
            "store.get"          => StorageService.Get(Str(payload, "key")),
            "store.set"          => StorageService.Set(Str(payload, "key"), Str(payload, "value")),
            "store.delete"       => StorageService.Delete(Str(payload, "key")),
            "store.clear"        => StorageService.Clear(),
            "system.info"        => await GetSystemInfoAsync(),
            "system.cpu"         => GetCpuUsage(),
            "system.ram"         => GetRamUsage(),
            "system.processes"   => GetProcessList(),
            "network.ping"       => await PingHostAsync(Str(payload, "host") ?? "8.8.8.8"),
            "network.flush"      => FlushDns(),
            "network.tcp"        => OptimizeTcp(),
            "boost.apply"        => ApplyBoost(Str(payload, "process")),
            "boost.revert"       => RevertBoost(),
            "process.kill"       => KillProcess(Int(payload, "pid")),
            "config.apiurl"      => "https://api.valcrown.com",
            "config.version"     => "1.0.0",
            _                    => (object?)null
        };
    }

    // ── SYSTEM ────────────────────────────────────────────────────────────────

    private static async Task<object> GetSystemInfoAsync()
    {
        var cpu = "Unknown CPU";
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo("powershell",
                    "-NoProfile -NonInteractive -Command \"(Get-CimInstance Win32_Processor | Select-Object -First 1).Name\"")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                }
            };
            proc.Start();
            cpu = (await proc.StandardOutput.ReadToEndAsync()).Trim();
            await proc.WaitForExitAsync();
        }
        catch { }

        var memInfo = GC.GetGCMemoryInfo();
        var totalRam = memInfo.TotalAvailableMemoryBytes / 1_073_741_824L; // bytes → GB

        return new
        {
            cpuModel = string.IsNullOrWhiteSpace(cpu) ? "Unknown CPU" : cpu,
            cpuCores = Environment.ProcessorCount,
            totalRam,
            hostname = Environment.MachineName,
            os       = Environment.OSVersion.VersionString
        };
    }

    private static double GetCpuUsage()
    {
        try
        {
            using var counter = new WinPerf("Processor", "% Processor Time", "_Total");
            counter.NextValue();
            Thread.Sleep(150);
            return Math.Round(counter.NextValue(), 1);
        }
        catch { return 0.0; }
    }

    private static double GetRamUsage()
    {
        try
        {
            var info = GC.GetGCMemoryInfo();
            if (info.TotalAvailableMemoryBytes == 0) return 0;
            return Math.Round((double)info.MemoryLoadBytes / info.TotalAvailableMemoryBytes * 100, 1);
        }
        catch { return 0.0; }
    }

    private static List<object> GetProcessList()
    {
        var list = new List<object>(64);
        try
        {
            foreach (var p in Process.GetProcesses().Take(60))
            {
                try
                {
                    list.Add(new
                    {
                        name     = p.ProcessName + ".exe",
                        pid      = p.Id,
                        memoryMb = p.WorkingSet64 / 1_048_576L
                    });
                }
                catch { /* skip inaccessible processes */ }
                finally { p.Dispose(); }
            }
        }
        catch { }
        return list;
    }

    // ── NETWORK ───────────────────────────────────────────────────────────────

    private static async Task<long> PingHostAsync(string host)
    {
        try
        {
            using var ping  = new Ping();
            var       reply = await ping.SendPingAsync(host, 3000);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : 999L;
        }
        catch { return 999L; }
    }

    private static bool FlushDns()
    {
        try { RunCmd("ipconfig", "/flushdns"); return true; }
        catch { return false; }
    }

    private static bool OptimizeTcp()
    {
        try
        {
            RunCmd("netsh", "int tcp set global autotuninglevel=normal");
            RunCmd("netsh", "int tcp set global rss=enabled");
            return true;
        }
        catch { return false; }
    }

    // ── BOOST ─────────────────────────────────────────────────────────────────

    private static bool ApplyBoost(string? processName)
    {
        try
        {
            RunCmd("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            if (!string.IsNullOrWhiteSpace(processName))
            {
                var name = processName.Replace(".exe", string.Empty, StringComparison.OrdinalIgnoreCase);
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try   { p.PriorityClass = ProcessPriorityClass.High; }
                    catch { /* skip if access denied */ }
                    finally { p.Dispose(); }
                }
            }
            return true;
        }
        catch { return false; }
    }

    private static bool RevertBoost()
    {
        try { RunCmd("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e"); return true; }
        catch { return false; }
    }

    // ── PROCESS ───────────────────────────────────────────────────────────────

    private static bool KillProcess(int pid)
    {
        if (pid <= 0) return false;
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill();
            return true;
        }
        catch { return false; }
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────

    private static void RunCmd(string cmd, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(cmd, args)
        {
            CreateNoWindow  = true,
            UseShellExecute = false
        });
        p?.WaitForExit(5000);
    }

    private static string? Str(JsonElement el, string key)
    {
        try { return el.GetProperty(key).GetString(); }
        catch { return null; }
    }

    private static int Int(JsonElement el, string key)
    {
        try { return el.GetProperty(key).GetInt32(); }
        catch { return 0; }
    }
}
