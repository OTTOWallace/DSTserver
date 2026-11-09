using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DSTserver.Models;

namespace DSTserver.Services;

/// <summary>存档备份与回档。</summary>
public static class BackupService
{
    /// <summary>备份根目录：exe 旁 backups\Cluster_N\。</summary>
    public static string BackupRoot(string clusterDir) =>
        Path.Combine(AppPaths.AppDir, "backups", Path.GetFileName(clusterDir));

    /// <summary>创建一次备份（Master/Caves 打包为 zip + 集群文件），返回备份目录。</summary>
    public static string CreateBackup(string clusterDir)
    {
        if (!Directory.Exists(clusterDir)) return "";
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string dir = Path.Combine(BackupRoot(clusterDir), stamp);
        Directory.CreateDirectory(dir);

        string master = Path.Combine(clusterDir, "Master");
        if (Directory.Exists(master))
            ZipSkipLocked(master, Path.Combine(dir, "Master.zip"));

        string caves = Path.Combine(clusterDir, "Caves");
        if (Directory.Exists(caves))
            ZipSkipLocked(caves, Path.Combine(dir, "Caves.zip"));

        foreach (var f in new[] { "cluster.ini", "cluster_token.txt" })
        {
            string src = Path.Combine(clusterDir, f);
            if (File.Exists(src))
            {
                try { File.Copy(src, Path.Combine(dir, f), true); } catch { }
            }
        }
        return dir;
    }

    /// <summary>
    /// 容错打包：跳过服务器运行中被独占锁定的文件（如 server_log.txt），
    /// 而不是像 CreateFromDirectory 那样整体失败。
    /// </summary>
    private static void ZipSkipLocked(string srcDir, string zipPath)
    {
        using var fs = new FileStream(zipPath, FileMode.Create);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var file in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(srcDir, file);
            if (rel.StartsWith("server_log.txt", StringComparison.OrdinalIgnoreCase) ||
                rel.StartsWith("server_chat_log.txt", StringComparison.OrdinalIgnoreCase))
            {
                continue; // 日志文件：运行时独占且回档无关紧要
            }
            try
            {
                using var s = new FileStream(file, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                var entry = zip.CreateEntry(rel, CompressionLevel.Optimal);
                using var es = entry.Open();
                s.CopyTo(es);
            }
            catch
            {
                // 个别文件被锁定，跳过不影响整体备份
            }
        }
    }

    /// <summary>列出某个集群的所有备份，按时间倒序。</summary>
    public static List<BackupInfo> ListBackups(string clusterDir)
    {
        var list = new List<BackupInfo>();
        string root = BackupRoot(clusterDir);
        if (!Directory.Exists(root)) return list;
        foreach (var dir in Directory.GetDirectories(root).OrderByDescending(d => d))
        {
            string name = Path.GetFileName(dir);
            if (DateTime.TryParseExact(name, "yyyyMMdd_HHmmss", null,
                    System.Globalization.DateTimeStyles.None, out var t))
            {
                list.Add(new BackupInfo { Path = dir, Time = t, Slot = Path.GetFileName(clusterDir) });
            }
        }
        return list;
    }

    /// <summary>用指定备份覆盖当前存档（调用前必须先停止服务器）。</summary>
    public static void Restore(string clusterDir, string backupDir)
    {
        if (!Directory.Exists(backupDir)) throw new DirectoryNotFoundException("备份目录不存在: " + backupDir);

        string master = Path.Combine(clusterDir, "Master");
        string caves = Path.Combine(clusterDir, "Caves");

        DeleteDir(master);
        DeleteDir(caves);

        string masterZip = Path.Combine(backupDir, "Master.zip");
        if (File.Exists(masterZip))
        {
            Directory.CreateDirectory(master);
            ZipFile.ExtractToDirectory(masterZip, master);
        }
        string cavesZip = Path.Combine(backupDir, "Caves.zip");
        if (File.Exists(cavesZip))
        {
            Directory.CreateDirectory(caves);
            ZipFile.ExtractToDirectory(cavesZip, caves);
        }
        foreach (var f in new[] { "cluster.ini", "cluster_token.txt" })
        {
            string src = Path.Combine(backupDir, f);
            if (File.Exists(src)) File.Copy(src, Path.Combine(clusterDir, f), true);
        }
    }

    /// <summary>删除一个备份目录。</summary>
    public static void DeleteBackup(string backupDir)
    {
        try
        {
            if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
        }
        catch { }
    }

    private static void DeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch { }
    }
}
