using System;
using System.IO;

namespace DSTserver.Services;

/// <summary>所有路径的集中计算。</summary>
public static class AppPaths
{
    public static string AppDir => AppContext.BaseDirectory;

    public static string SteamCmdDir(string? custom) =>
        string.IsNullOrWhiteSpace(custom)
            ? Path.Combine(AppDir, "runtime", "steamcmd")
            : custom;

    public static string SteamCmdExe(string steamCmdDir) =>
        Path.Combine(steamCmdDir, "steamcmd.exe");

    public static string ServerDir(string steamCmdDir) =>
        Path.Combine(steamCmdDir, "steamapps", "common", "Don't Starve Together Dedicated Server");

    public static string ServerExe(string steamCmdDir)
    {
        string x64 = Path.Combine(ServerDir(steamCmdDir), "bin64", "dontstarve_dedicated_server_nullrenderer_x64.exe");
        if (File.Exists(x64)) return x64;
        return Path.Combine(ServerDir(steamCmdDir), "bin", "dontstarve_dedicated_server_nullrenderer.exe");
    }

    public static string ServerModsDir(string steamCmdDir) =>
        Path.Combine(ServerDir(steamCmdDir), "mods");

    public static string KleiRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Klei", "DoNotStarveTogether");

    public static string ClusterDir(int slot) =>
        Path.Combine(KleiRoot, $"Cluster_{slot}");

    public static string ClusterName(int slot) => $"Cluster_{slot}";

    public static string WorkshopContentDir(string steamCmdDir, string modId) =>
        Path.Combine(steamCmdDir, "steamapps", "workshop", "content", "322330", modId);
}
