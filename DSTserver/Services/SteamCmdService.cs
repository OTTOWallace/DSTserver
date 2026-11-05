using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DSTserver.Services;

/// <summary>SteamCMD：下载安装、服务器更新、创意工坊 mod 下载。</summary>
public static class SteamCmdService
{
    public const string SteamCmdUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
    public const string ServerAppId = "343050";    // 饥荒联机版专用服务器
    public const string WorkshopAppId = "322330";  // 饥荒联机版本体（工坊内容归属）

    public static async Task EnsureInstalledAsync(string steamCmdDir, IProgress<string> progress, CancellationToken ct)
    {
        if (File.Exists(Path.Combine(steamCmdDir, "steamcmd.exe"))) return;
        Directory.CreateDirectory(steamCmdDir);
        string zip = Path.Combine(steamCmdDir, "steamcmd.zip");
        progress.Report("正在下载 SteamCMD（约 3 MB）...");
        using (var hc = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
        {
            using var resp = await hc.GetAsync(SteamCmdUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            using (var fs = File.Create(zip))
            using (var stream = await resp.Content.ReadAsStreamAsync(ct))
            {
                await stream.CopyToAsync(fs, 81920, ct);
            }
        }
        progress.Report("正在解压 SteamCMD...");
        await Task.Run(() => ZipFile.ExtractToDirectory(zip, steamCmdDir, true), ct);
        File.Delete(zip);
        progress.Report("SteamCMD 就绪");
    }

    public static async Task<int> RunAsync(string steamCmdDir, string args, IProgress<string> progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(steamCmdDir, "steamcmd.exe"),
            Arguments = args,
            WorkingDirectory = steamCmdDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (s, e) => { if (e.Data != null && IsUsefulLine(e.Data)) progress.Report(e.Data); };
        proc.ErrorDataReceived += (s, e) => { if (e.Data != null && IsUsefulLine(e.Data)) progress.Report(e.Data); };
        if (!proc.Start()) return -1;
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct);
        return proc.ExitCode;
    }

    /// <summary>过滤 steamcmd 的控制台横幅噪声，只保留有用信息。</summary>
    private static bool IsUsefulLine(string line)
    {
        string t = line.Trim();
        if (t.Length == 0) return false;
        if (t == "-- type 'quit' to exit --") return false;
        if (t.StartsWith("Steam Console Client (c) Valve Corporation")) return false;
        if (t.StartsWith("Loading Steam API") || t.StartsWith("Unloading Steam API")) return false;
        if (t.StartsWith("Connecting anonymously to Steam Public")) return false;
        if (t.StartsWith("Waiting for client config") || t.StartsWith("Waiting for user info")) return false;
        if (t == "OK") return false;
        if (t.StartsWith("Redirecting stderr to")) return false;
        if (t.StartsWith("Logging directory:")) return false;
        if (t.StartsWith("[  0%] Checking for available updates")) return false;
        if (t.StartsWith("[----] Verifying installation")) return false;
        if (t.StartsWith("CWorkThreadPool::~")) return false;
        return true;
    }

    public static async Task InstallOrUpdateServerAsync(string steamCmdDir, IProgress<string> progress, CancellationToken ct)
    {
        await EnsureInstalledAsync(steamCmdDir, progress, ct);
        progress.Report("正在安装 / 更新 饥荒联机版专用服务器（首次约 3~4 GB，视网速而定，请耐心等待）...");
        int code = await RunAsync(steamCmdDir, $"+login anonymous +app_update {ServerAppId} validate +quit", progress, ct);
        progress.Report(code == 0 ? "服务器安装 / 更新完成" : $"steamcmd 退出码: {code}");
    }

    public static async Task<int> DownloadWorkshopAsync(string steamCmdDir, string modId, IProgress<string> progress, CancellationToken ct)
    {
        await EnsureInstalledAsync(steamCmdDir, progress, ct);

        // 清掉工坊缓存记录：否则当内容目录缺失时 steamcmd 会基于缓存报“假成功”而不落盘。
        try
        {
            string workshopDir = Path.Combine(steamCmdDir, "steamapps", "workshop");
            if (Directory.Exists(workshopDir))
            {
                foreach (var acf in Directory.GetFiles(workshopDir, "appworkshop_*.acf"))
                    File.Delete(acf);
            }
        }
        catch { }

        progress.Report($"正在从创意工坊下载 mod {modId} ...");
        int code = await RunAsync(steamCmdDir, $"+login anonymous +workshop_download_item {WorkshopAppId} {modId} +quit", progress, ct);
        return code;
    }
}
