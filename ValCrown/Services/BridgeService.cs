using System.Diagnostics;
using System.Text.Json;
using System.Net.NetworkInformation;

namespace ValCrown.Services;

public static class BridgeService
{
    public static async Task<object?> Handle(string action, JsonElement payload)
    {
        switch (action)
        {
            case "store.get":    return StorageService.Get(GetStr(payload, "key"));
            case "store.set":    return StorageService.Set(GetStr(payload, "key"), GetStr(payload, "value"));
            case "store.delete": return StorageService.Delete(GetStr(payload, "key"));
            case "store.clear":  return StorageService.Clear();
            case "system.info":  return await GetSystemInfo();
            case "system.cpu":   return GetCpuUsage();
            case "system.ram":   return GetRamUsage();
            case "system.processes": return GetProcesses();
            case "network.ping": return await PingHost(TryGetStr(payload, "host") ?? "8.8.8.8");
            case "network.flush": return FlushDns();
            case "network.tcp":  return OptimizeTcp();
            case "boost.apply":  return ApplyBoost(TryGetStr(payload, "process"));
            case "boost.revert": return RevertBoost();
            case "process.kill": return KillProcess(GetInt(payload, "pid"));
            case "config.apiurl": return "https://api.valcrown.com";
            case "config.version": return "1.0.0";
            default: return null;
        }
    }

    private static async Task<object> GetSystemInfo()
    {
        var cpu = "Unknown CPU";
        try
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo("powershell",
                "-NoProfile -Command \"(Get-WmiObject Win32_Processor | Select -First 1).Name\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            p.Start();
            cpu = (await p.StandardOutput.ReadToEndAsync()).Trim();
            await p.WaitForExitAsync();
        }
        catch { }

        return new
        {
            cpuModel = cpu,
            cpuCores = Environment.ProcessorCount,
            totalRam = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024 / 1024,
            freeRam  = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024 / 1024,
            hostname = Environment.MachineName,
            os       = Environment.OSVersion.VersionString
        };
    }

    private static float GetCpuUsage()
    {
        try
        {
            using var c = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            c.NextValue();
            Thread.Sleep(100);
            return c.NextValue();
        }
        catch { return 0; }
    }

    private static double GetRamUsage()
    {
        var info = GC.GetGCMemoryInfo();
        if (info.TotalAvailableMemoryBytes == 0) return 0;
        return (double)info.MemoryLoadBytes / info.TotalAvailableMemoryBytes * 100;
    }

    private static object GetProcesses()
    {
        var list = new List<object>();
        foreach (var p in Process.GetProcesses().Take(60))
        {
            try
            {
                list.Add(new
                {
                    name = p.ProcessName + ".exe",
                    pid  = p.Id,
                    memoryMb = p.WorkingSet64 / 1024 / 1024
                });
            }
            catch { }
        }
        return list;
    }

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
        try { Run("ipconfig", "/flushdns"); return true; }
        catch { return false; }
    }

    private static bool OptimizeTcp()
    {
        try
        {
            Run("netsh", "int tcp set global autotuninglevel=normal");
            Run("netsh", "int tcp set global rss=enabled");
            return true;
        }
        catch { return false; }
    }

    private static bool ApplyBoost(string? processName)
    {
        try
        {
            Run("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            if (!string.IsNullOrEmpty(processName))
            {
                var name = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                foreach (var p in Process.GetProcessesByName(name))
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
        try { Run("powercfg", "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e"); return true; }
        catch { return false; }
    }

    private static bool KillProcess(int pid)
    {
        try { Process.GetProcessById(pid).Kill(); return true; }
        catch { return false; }
    }

    private static void Run(string cmd, string args)
    {
        Process.Start(new ProcessStartInfo(cmd, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        })?.WaitForExit();
    }

    private static string? GetStr(JsonElement el, string key)
    {
        try { return el.GetProperty(key).GetString(); } catch { return null; }
    }

    private static string? TryGetStr(JsonElement el, string key)
    {
        try { return el.GetProperty(key).GetString(); } catch { return null; }
    }

    private static int GetInt(JsonElement el, string key)
    {
        try { return el.GetProperty(key).GetInt32(); } catch { return 0; }
    }
}
