using System;
using System.IO;
using System.Text.Json;
using DSTserver.Models;

namespace DSTserver.Services;

/// <summary>启动器自身配置（持久化在 exe 旁的 settings.json）。</summary>
public class AppSettings
{
    public string SteamCmdDir { get; set; } = "";
    public ClusterSettings Cluster { get; set; } = new();

    // 定时任务
    public bool EnableScheduledBackup { get; set; } = true;
    public int BackupIntervalHours { get; set; } = 24;
    public bool EnableModAutoCheck { get; set; } = true;
    public int ModCheckIntervalHours { get; set; } = 24;
    public DateTime LastBackupUtc { get; set; } = DateTime.MinValue;
    public DateTime LastModCheckUtc { get; set; } = DateTime.MinValue;

    private static string FilePath => Path.Combine(AppPaths.AppDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (s != null)
                {
                    s.Cluster ??= new ClusterSettings();
                    return s;
                }
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
