using System.Text.Json;

namespace ValCrown.Services;

/// <summary>
/// Central bridge router. Every JS call comes here.
/// Never throws — always returns a result object.
/// </summary>
public static class BridgeService
{
    public static async Task<object?> Handle(string action, JsonElement payload)
    {
        try
        {
            return action switch
            {
                // Storage
                "store.get"            => StorageService.Get(S(payload, "key")),
                "store.set"            => StorageService.Set(S(payload, "key"), S(payload, "value")),
                "store.delete"         => StorageService.Delete(S(payload, "key")),
                "store.clear"          => StorageService.Clear(),

                // System
                "system.info"          => await SystemService.GetSystemInfo(),
                "system.cpu"           => SystemService.GetCpuUsage(),
                "system.ram"           => SystemService.GetRamInfo(),
                "system.processes"     => SystemService.GetProcesses(),
                "system.cleanram"      => SystemService.CleanRam(),
                "system.anticheat"     => SystemService.CheckAntiCheat(),
                "system.startup"       => SystemService.SetStartup(B(payload, "enabled")),
                "system.startupenabled"=> SystemService.GetStartupEnabled(),

                // Process
                "process.kill"         => SystemService.KillProcess(I(payload, "pid")),

                // Network
                "network.ping"         => await NetworkService.PingHost(S(payload, "host") ?? "8.8.8.8"),
                "network.bestping"     => await NetworkService.GetBestPing(),
                "network.flush"        => NetworkService.FlushDns(),
                "network.tcp"          => NetworkService.OptimizeTcp(),
                "network.info"         => NetworkService.GetNetworkInfo(),
                "network.setdns"       => NetworkService.SetDns(S(payload, "primary") ?? "8.8.8.8", S(payload, "secondary") ?? "8.8.4.4"),

                // Boost
                "boost.apply"          => await BoostService.Apply(S(payload, "process"), S(payload, "mode") ?? "safe"),
                "boost.revert"         => BoostService.Revert(),
                "boost.gpu"            => BoostService.GetGpuInfo(),
                "boost.isactive"       => BoostService.IsActive,

                // Config
                "config.apiurl"        => "https://api.valcrown.com",
                "config.version"       => GetVersion(),

                _                      => (object?)null
            };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    private static string GetVersion()
        => System.Reflection.Assembly.GetExecutingAssembly()
               .GetName().Version?.ToString(3) ?? "1.0.0";

    private static string? S(JsonElement el, string key)
    {
        try { return el.GetProperty(key).GetString(); } catch { return null; }
    }

    private static int I(JsonElement el, string key)
    {
        try { return el.GetProperty(key).GetInt32(); } catch { return 0; }
    }

    private static bool B(JsonElement el, string key)
    {
        try { return el.GetProperty(key).GetBoolean(); } catch { return false; }
    }
}
