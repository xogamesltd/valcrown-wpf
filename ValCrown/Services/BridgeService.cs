using System.Diagnostics;
using System.Text.Json;
using System.Net.NetworkInformation;
using ValCrown.Services;

namespace ValCrown.Services;

public static class BridgeService
{
    public static async Task<object?> Handle(string action, JsonElement payload)
    {
        return action switch
        {
            // Storage
            "store.get"    => StorageService.Get(payload.GetString("key")),
            "store.set"    => StorageService.Set(payload.GetString("key"), payload.GetString("value")),
            "store.delete" => StorageService.Delete(payload.GetString("key")),
            "store.clear"  => StorageService.Clear(),

            // System
            "system.info"     => await GetSystemInfo(),
            "system.cpu"      => GetCpuUsage(),
            "system.ram"      => GetRamUsage(),
            "system.processes"=> GetProcesses(),

            // Network
            "network.ping"    => await PingHost(payload.TryGetString("host") ?? "8.8.8.8"),
            "network.flush"   => FlushDns(),
            "network.tcp"     => OptimizeTcp(),

            // Boost
            "boost.apply"     => ApplyBoost(payload.TryGetString("process")),
            "boost.revert"    => RevertBoost(),

            // Process
            "process.kill"    => KillProcess(payload.GetInt32("pid")),

            // Config
            "config.apiurl"   => "https://api.valcrown.com",
            "config.version"  => GetVersion(),

            _ => null
        };
    }

    // ── SYSTEM ────────────────────────────────────────────────────────────────
    private static async Task<object> GetSystemInfo()
    {
        var cpu = "";
        var cores = 0;
        try
        {
            var p = new Process { StartInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"Get-WmiObject Win32_Processor | Select-Object -First 1 -ExpandProperty Name\"",
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            }};
            p.Start();
            cpu = (await p.StandardOutput.ReadToEndAsync()).Trim();
            await p.WaitForExitAsync();
        }
        catch { cpu = "Unknown CPU"; }

        cores = Environment.ProcessorCount;
        var totalRam = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024 / 1024;

        return new
        {
            cpuModel  = cpu,
            cpuCores  = cores,
            totalRam  = totalRam,
            freeRam   = totalRam - (GC.GetTotalMemory(false) / 1024 / 1024 / 1024),
            hostname  = Environment.MachineName,
            os        = Environment.OSVersion.VersionString
        };
    }

    private static float GetCpuUsage()
    {
        try
        {
            using var counter = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");
            counter.NextValue();
            System.Threading.Thread.Sleep(100);
            return counter.NextValue();
        }
        catch { return 0; }
    }

    private static object GetRamUsage()
    {
        var info = GC.GetGCMemoryInfo();
        var total = info.TotalAvailableMemoryBytes;
        var avail = info.MemoryLoadBytes;
        if (total == 0) return 0;
        return (double)avail / total * 100;
    }

    private static object GetProcesses()
    {
        return Process.GetProcesses()
            .Where(p => { try { return p.MainWindowTitle.Length > 0 || p.ProcessName.Length > 0; } catch { return false; } })
            .Take(60)
            .Select(p =>
            {
                try { return new { name = p.ProcessName + ".exe", pid = p.Id, memoryMb = p.WorkingSet64 / 1024 / 1024 }; }
                catch { return null; }
            })
            .Where(p => p != null)
            .ToList();
    }

    // ── NETWORK ───────────────────────────────────────────────────────────────
    private static async Task<long> PingHost(string host)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 3000);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : 999;
        }
        catch { return 999; }
    }

    private static bool FlushDns()
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns")
            { CreateNoWindow = true, UseShellExecute = false });
            p?.WaitForExit();
            return true;
        }
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
            // High Performance power plan
            RunCmd("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

            // Set process priority
            if (!string.IsNullOrEmpty(processName))
            {
                var procs = Process.GetProcessesByName(
                    processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase));
                foreach (var p in procs)
                {
                    try { p.PriorityClass = ProcessPriorityClass.High; } catch { }
                }
            }
            return true;
        }
        catch { return false; }
    }

    private static bool RevertBoost()
    {
        try
        {
            RunCmd("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
            return true;
        }
        catch { return false; }
    }

    // ── PROCESS ───────────────────────────────────────────────────────────────
    private static bool KillProcess(int pid)
    {
        try { Process.GetProcessById(pid).Kill(); return true; }
        catch { return false; }
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────
    private static string GetVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString() ?? "1.0.0";
    }

    private static void RunCmd(string cmd, string args)
    {
        Process.Start(new ProcessStartInfo(cmd, args)
        { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit();
    }
}

// Extension helpers
public static class JsonElementExtensions
{
    public static string? TryGetString(this JsonElement el, string key)
    {
        try { return el.GetProperty(key).GetString(); } catch { return null; }
    }

    public static string GetString(this JsonElement el, string key)
    {
        return el.GetProperty(key).GetString() ?? "";
    }

    public static int GetInt32(this JsonElement el, string key)
    {
        return el.GetProperty(key).GetInt32();
    }
}
