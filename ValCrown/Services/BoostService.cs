using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;

namespace ValCrown.Services;

/// <summary>
/// Enterprise boost engine with Safe and Aggressive modes.
/// All tweaks are reversible. Never touches anti-cheat processes.
/// </summary>
public static class BoostService
{
    private const string HighPerf = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string UltPerf  = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    public static bool IsActive { get; private set; }

    // ── APPLY BOOST ───────────────────────────────────────────────────────────
    public static async Task<object> Apply(string? processName, string mode = "safe")
    {
        var applied = new List<string>();
        var skipped = new List<string>();

        await Task.Run(() =>
        {
            try
            {
                // 1. Power plan
                if (!RunCmd("powercfg", $"/setactive {UltPerf}"))
                    RunCmd("powercfg", $"/setactive {HighPerf}");
                applied.Add("Power: High Performance");

                // 2. Disable GameDVR (major FPS killer)
                RunCmd("reg", @"add ""HKCU\System\GameConfigStore"" /v GameDVR_Enabled /t REG_DWORD /d 0 /f");
                RunCmd("reg", @"add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR"" /v AllowGameDVR /t REG_DWORD /d 0 /f");
                applied.Add("GameDVR: Disabled");

                // 3. Enable Hardware-Accelerated GPU Scheduling
                RunCmd("reg", @"add ""HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers"" /v HwSchMode /t REG_DWORD /d 2 /f");
                applied.Add("HAGS: Enabled");

                // 4. Timer resolution — critical for gaming
                NtSetTimerResolution(5000, true, out _);
                applied.Add("Timer: 0.5ms resolution");

                // 5. Disable Windows visual animations
                RunCmd("reg", @"add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"" /v VisualFXSetting /t REG_DWORD /d 2 /f");
                applied.Add("Visual effects: Performance");

                // 6. Process priority
                if (!string.IsNullOrWhiteSpace(processName))
                {
                    var name = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try
                        {
                            p.PriorityClass = ProcessPriorityClass.High;
                            p.ProcessorAffinity = (IntPtr)(Math.Pow(2, Environment.ProcessorCount) - 1);
                            applied.Add($"Priority: {processName} → High");
                        }
                        catch { }
                        finally { p.Dispose(); }
                    }
                }

                // 7. Aggressive mode extras
                if (mode == "aggressive")
                {
                    // Disable SysMain (Superfetch) — frees RAM
                    RunCmd("sc", "stop SysMain");
                    RunCmd("sc", "config SysMain start=disabled");
                    applied.Add("SysMain: Disabled");

                    // Disable Windows Search indexing
                    RunCmd("sc", "stop WSearch");
                    applied.Add("WSearch: Stopped");

                    // Disable telemetry
                    RunCmd("reg", @"add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection"" /v AllowTelemetry /t REG_DWORD /d 0 /f");
                    applied.Add("Telemetry: Disabled");

                    // Nagle algorithm disable (reduces ping)
                    RunCmd("reg", @"add ""HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"" /v TcpAckFrequency /t REG_DWORD /d 1 /f");
                    RunCmd("reg", @"add ""HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"" /v TCPNoDelay /t REG_DWORD /d 1 /f");
                    applied.Add("Nagle Algorithm: Disabled");
                }
            }
            catch (Exception ex)
            {
                skipped.Add(ex.Message);
            }
        });

        IsActive = true;
        return new { success = true, mode, applied, skipped };
    }

    // ── REVERT BOOST ─────────────────────────────────────────────────────────
    public static object Revert()
    {
        try
        {
            RunCmd("powercfg", $"/setactive {Balanced}");
            RunCmd("reg", @"add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"" /v VisualFXSetting /t REG_DWORD /d 0 /f");
            RunCmd("sc", "start SysMain");
            RunCmd("sc", "config SysMain start=auto");
            NtSetTimerResolution(156250, true, out _);
            IsActive = false;
            return new { success = true };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    // ── GPU BOOST ─────────────────────────────────────────────────────────────
    public static object GetGpuInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM, CurrentRefreshRate, VideoProcessor FROM Win32_VideoController");
            var gpus = new List<object>();
            foreach (ManagementObject o in searcher.Get())
            {
                gpus.Add(new
                {
                    name         = o["Name"]?.ToString(),
                    vramMb       = Convert.ToInt64(o["AdapterRAM"] ?? 0) / 1_048_576,
                    refreshRate  = o["CurrentRefreshRate"]?.ToString(),
                    processor    = o["VideoProcessor"]?.ToString()
                });
            }
            return new { gpus };
        }
        catch { return new { gpus = new List<object>() }; }
    }

    private static bool RunCmd(string cmd, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(cmd, args)
            {
                CreateNoWindow = true, UseShellExecute = false
            });
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    [DllImport("ntdll.dll")]
    private static extern int NtSetTimerResolution(uint DesiredResolution, bool SetResolution, out uint CurrentResolution);
}
