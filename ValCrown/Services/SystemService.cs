using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace ValCrown.Services;

/// <summary>
/// Enterprise-grade system monitoring and optimization.
/// Uses WMI for accurate hardware data. All methods are safe — never crash.
/// </summary>
public static class SystemService
{
    private static PerformanceCounter? _cpuCounter;
    private static DateTime _lastCpuRead = DateTime.MinValue;
    private static double _lastCpuValue = 0;

    // Anti-cheat process names — never touch these
    private static readonly HashSet<string> AntiCheatProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "easyanticheat.exe", "battleye.exe", "be_service.exe", "vgc.exe", "vanguard.exe",
        "faceitclient.exe", "esea.exe", "punkbuster.exe", "pbsvc.exe", "eac_launcher.exe",
        "anticheatsdk_launcher.exe", "gamescanner.exe"
    };

    // System-critical processes — never kill
    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe",
        "services.exe", "lsass.exe", "svchost.exe", "dwm.exe", "explorer.exe",
        "taskmgr.exe", "valcrown.exe"
    };

    // ── CPU ───────────────────────────────────────────────────────────────────
    public static double GetCpuUsage()
    {
        try
        {
            // Cache for 500ms to avoid hammering
            if ((DateTime.UtcNow - _lastCpuRead).TotalMilliseconds < 500)
                return _lastCpuValue;

            _cpuCounter ??= new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();
            System.Threading.Thread.Sleep(100);
            _lastCpuValue = Math.Round(_cpuCounter.NextValue(), 1);
            _lastCpuRead  = DateTime.UtcNow;
            return _lastCpuValue;
        }
        catch { return 0; }
    }

    // ── RAM ───────────────────────────────────────────────────────────────────
    public static object GetRamInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var totalKb = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                var freeKb  = Convert.ToDouble(obj["FreePhysicalMemory"]);
                var totalGb = Math.Round(totalKb / 1024 / 1024, 1);
                var freeGb  = Math.Round(freeKb  / 1024 / 1024, 1);
                var usedGb  = Math.Round(totalGb - freeGb, 1);
                var usedPct = Math.Round(usedGb / totalGb * 100, 1);
                return new { totalGb, usedGb, freeGb, usedPct };
            }
        }
        catch { }
        return new { totalGb = 0.0, usedGb = 0.0, freeGb = 0.0, usedPct = 0.0 };
    }

    // ── CLEAN RAM ─────────────────────────────────────────────────────────────
    public static object CleanRam()
    {
        try
        {
            // Force GC
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Empty working sets of non-critical processes
            var cleaned = 0;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (!ProtectedProcesses.Contains(p.ProcessName + ".exe") &&
                        !AntiCheatProcesses.Contains(p.ProcessName + ".exe"))
                    {
                        EmptyWorkingSet(p.Handle);
                        cleaned++;
                    }
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
            return new { success = true, processescleaned = cleaned };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    // ── SYSTEM INFO ───────────────────────────────────────────────────────────
    public static async Task<object> GetSystemInfo()
    {
        var cpu = "Unknown CPU";
        var gpu = "Unknown GPU";
        var gpuVram = 0L;
        var gpuVendor = "Unknown";

        await Task.Run(() =>
        {
            try
            {
                using var cpuSearch = new ManagementObjectSearcher("SELECT Name, NumberOfCores, MaxClockSpeed FROM Win32_Processor");
                foreach (ManagementObject o in cpuSearch.Get())
                {
                    cpu = o["Name"]?.ToString()?.Trim() ?? cpu;
                    break;
                }
            }
            catch { }

            try
            {
                using var gpuSearch = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
                foreach (ManagementObject o in gpuSearch.Get())
                {
                    var name = o["Name"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(name) && !name.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                    {
                        gpu = name;
                        gpuVram = Convert.ToInt64(o["AdapterRAM"] ?? 0) / 1024 / 1024;
                        gpuVendor = name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ? "NVIDIA" :
                                    name.Contains("AMD",    StringComparison.OrdinalIgnoreCase) ? "AMD"    :
                                    name.Contains("Intel",  StringComparison.OrdinalIgnoreCase) ? "Intel"  : "Unknown";
                        break;
                    }
                }
            }
            catch { }
        });

        var ramInfo = GetRamInfo();
        var osVer   = Environment.OSVersion;
        var build   = osVer.Version.Build;
        var osName  = build >= 22000 ? "Windows 11" : "Windows 10";

        return new
        {
            cpuModel   = cpu,
            cpuCores   = Environment.ProcessorCount,
            gpu,
            gpuVram,
            gpuVendor,
            totalRam   = ((dynamic)ramInfo).totalGb,
            freeRam    = ((dynamic)ramInfo).freeGb,
            hostname   = Environment.MachineName,
            username   = Environment.UserName,
            os         = osName,
            osBuild    = build,
            osVersion  = osVer.VersionString,
            is64Bit    = Environment.Is64BitOperatingSystem,
            dotnetVer  = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        };
    }

    // ── PROCESSES ─────────────────────────────────────────────────────────────
    public static List<object> GetProcesses()
    {
        var list = new List<object>(80);
        try
        {
            var procs = Process.GetProcesses()
                .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0L; } })
                .Take(60);

            foreach (var p in procs)
            {
                try
                {
                    var isProtected  = ProtectedProcesses.Contains(p.ProcessName + ".exe");
                    var isAntiCheat  = AntiCheatProcesses.Contains(p.ProcessName + ".exe");
                    list.Add(new
                    {
                        name        = p.ProcessName + ".exe",
                        pid         = p.Id,
                        memoryMb    = p.WorkingSet64 / 1_048_576L,
                        isProtected = isProtected || isAntiCheat,
                        isAntiCheat,
                        canKill     = !isProtected && !isAntiCheat
                    });
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
        }
        catch { }
        return list;
    }

    // ── KILL PROCESS ──────────────────────────────────────────────────────────
    public static object KillProcess(int pid)
    {
        if (pid <= 4) return new { success = false, reason = "System process" };
        try
        {
            using var p = Process.GetProcessById(pid);
            if (ProtectedProcesses.Contains(p.ProcessName + ".exe"))
                return new { success = false, reason = "Protected process" };
            if (AntiCheatProcesses.Contains(p.ProcessName + ".exe"))
                return new { success = false, reason = "Anti-cheat process — cannot kill" };
            p.Kill(entireProcessTree: true);
            return new { success = true };
        }
        catch (Exception ex)
        {
            return new { success = false, reason = ex.Message };
        }
    }

    // ── STARTUP MANAGEMENT ────────────────────────────────────────────────────
    public static object SetStartup(bool enable)
    {
        try
        {
            var key  = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            var exe  = System.AppContext.BaseDirectory + "ValCrown.exe";
            if (enable)
                key?.SetValue("ValCrown", $"\"{exe}\"");
            else
                key?.DeleteValue("ValCrown", throwOnMissingValue: false);
            key?.Dispose();
            return new { success = true, enabled = enable };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    public static bool GetStartupEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("ValCrown") != null;
        }
        catch { return false; }
    }

    // ── ANTI-CHEAT DETECTION ─────────────────────────────────────────────────
    public static object CheckAntiCheat()
    {
        var detected = new List<string>();
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (AntiCheatProcesses.Contains(p.ProcessName + ".exe"))
                        detected.Add(p.ProcessName);
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
        }
        catch { }
        return new
        {
            detected,
            safe    = detected.Count == 0,
            warning = detected.Count > 0
                ? $"Anti-cheat detected: {string.Join(", ", detected)}. ValCrown will NOT touch these processes."
                : "No anti-cheat detected — safe to boost"
        };
    }
}
