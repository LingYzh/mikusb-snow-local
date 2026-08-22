using System.Diagnostics;
using System.Net.NetworkInformation;

namespace MikuSB.Util;

public static class OfficialIpLoopback
{
    private static readonly Logger Logger = new("Loopback");

    private static readonly string[] OfficialIps =
    [
        "42.192.24.211"
    ];

    private static readonly string[] LocalHostNames =
    [
        "cbjq-serverlist.xoyocdn.com",
        "cbjq-client.xoyocdn.com",
        "cbjq-client-qq.xoyocdn.com",
        "cbjq-client-hsyq.xoyocdn.com",
        "js2sdk.xoyo.com",
        "sh-jxsj.xgsdk.com",
        "passport.xoyo.com",
        "xgsdk.xoyo.games",
        "xqdata.xoyo.games",
        "sh-qrcode.xoyo.com"
    ];

    public static void TryBind()
    {
        foreach (var ip in OfficialIps)
        {
            if (IsLocalAddress(ip))
            {
                Logger.Info($"Official login IP already on this machine: {ip}");
                continue;
            }

            var result = RunNetsh($"interface ipv4 add address \"Loopback Pseudo-Interface 1\" {ip} 255.255.255.255");
            if (result.ExitCode == 0 || LooksAlreadyBound(result.Output))
            {
                Logger.Info($"Bound official login IP to loopback: {ip}");
                continue;
            }

            Logger.Warn($"Could not bind {ip} to loopback (need Administrator). Game TCP to this IP will still hit official servers. Run 绑定官方登录IP到本地.bat as admin. netsh: {result.Output}");
        }

        TryWriteHosts();
        TryAddHttpsPortProxy();
    }

    private static void TryWriteHosts()
    {
        try
        {
            var hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
            var lines = File.Exists(hostsPath)
                ? File.ReadAllLines(hostsPath).ToList()
                : [];

            var kept = new List<string>();
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Contains("# MikuSB", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (trimmed.Contains("# Snowbreak Local Server", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (IsSnowbreakMegaLine(trimmed) || IsManagedHostLine(trimmed))
                    continue;
                kept.Add(line);
            }

            while (kept.Count > 0 && string.IsNullOrWhiteSpace(kept[^1]))
                kept.RemoveAt(kept.Count - 1);

            kept.Add("");
            kept.Add("# Snowbreak Local Server");
            foreach (var host in LocalHostNames)
                kept.Add($"127.0.0.1 {host} # MikuSB");

            File.WriteAllLines(hostsPath, kept);
            Logger.Info($"Wrote {LocalHostNames.Length} hosts entries (one hostname per line)");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Could not update hosts file (need Administrator): {ex.Message}");
        }
    }

    private static bool IsSnowbreakMegaLine(string trimmed)
    {
        return trimmed.StartsWith("127.0.0.1", StringComparison.Ordinal)
               && trimmed.Contains("sh-jxsj.xgsdk.com", StringComparison.OrdinalIgnoreCase)
               && trimmed.Contains("js2sdk.xoyo.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedHostLine(string trimmed)
    {
        if (!trimmed.StartsWith("127.0.0.1", StringComparison.Ordinal) &&
            !trimmed.StartsWith("::1", StringComparison.Ordinal))
            return false;

        foreach (var host in LocalHostNames)
        {
            if (trimmed.Contains(host, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void TryAddHttpsPortProxy()
    {
        AddPortProxy(443, 13443);
        AddPortProxy(18443, 13443);
    }

    private static void AddPortProxy(int listenPort, int connectPort)
    {
        var result = RunNetsh($"interface portproxy add v4tov4 listenaddress=127.0.0.1 listenport={listenPort} connectaddress=127.0.0.1 connectport={connectPort}");
        if (result.ExitCode == 0 || LooksAlreadyBound(result.Output))
        {
            Logger.Info($"HTTPS portproxy 127.0.0.1:{listenPort} -> 127.0.0.1:{connectPort} ready");
            return;
        }

        Logger.Warn($"Could not add HTTPS portproxy {listenPort}->{connectPort} (need Administrator). netsh: {result.Output}");
    }

    private static bool IsLocalAddress(string ip)
    {
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (var addr in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.ToString() == ip)
                        return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool LooksAlreadyBound(string output)
    {
        return output.Contains("对象已存在", StringComparison.OrdinalIgnoreCase)
               || output.Contains("already exists", StringComparison.OrdinalIgnoreCase)
               || output.Contains("exists", StringComparison.OrdinalIgnoreCase);
    }

    private static (int ExitCode, string Output) RunNetsh(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process == null)
                return (-1, "failed to start netsh");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(8000);
            return (process.ExitCode, (stdout + stderr).Trim());
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
