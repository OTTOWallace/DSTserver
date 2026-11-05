using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSTserver.Models;

namespace DSTserver.Services;

/// <summary>mod 增删、启用停用、配置读写。</summary>
public class ModService
{
    private readonly string _steamCmdDir;
    private readonly string _modsDir;
    private readonly string _clusterDir;

    public ModService(string steamCmdDir, string clusterDir)
    {
        _steamCmdDir = steamCmdDir;
        _modsDir = AppPaths.ServerModsDir(steamCmdDir);
        _clusterDir = clusterDir;
    }

    public string ModsDir => _modsDir;
    public string SetupLuaPath => Path.Combine(_modsDir, "dedicated_server_mods_setup.lua");
    public string MasterDir => Path.Combine(_clusterDir, "Master");
    public string CavesDir => Path.Combine(_clusterDir, "Caves");

    public List<InstalledMod> ListInstalled()
    {
        var setupIds = LuaFileIo.ParseModSetupLua(SetupLuaPath);
        var overrides = LuaFileIo.ParseModOverrides(Path.Combine(MasterDir, "modoverrides.lua"));
        var list = new List<InstalledMod>();
        var seen = new HashSet<string>();

        string ugc = Path.Combine(AppPaths.ServerDir(_steamCmdDir), "ugc_mods",
            Path.GetFileName(_clusterDir), "Master", "content", "322330");

        foreach (var id in setupIds)
        {
            string folder = Path.Combine(_modsDir, "workshop-" + id);
            string ugcFolder = Path.Combine(ugc, id);
            if (!File.Exists(Path.Combine(folder, "modinfo.lua")) &&
                File.Exists(Path.Combine(ugcFolder, "modinfo.lua")))
            {
                folder = ugcFolder; // 服务器运行后 mod 被迁移到 ugc_mods
            }
            if (seen.Add(id)) list.Add(Make(id, overrides, folder));
        }
        if (Directory.Exists(_modsDir))
        {
            foreach (var dir in Directory.GetDirectories(_modsDir))
            {
                string name = Path.GetFileName(dir);
                if (!name.StartsWith("workshop-")) continue;
                string id = name.Substring("workshop-".Length);
                if (seen.Add(id)) list.Add(Make(id, overrides, dir));
            }
        }
        // 服务器运行后会把 mod 迁移到 ugc_mods\Cluster_N\Master\content\322330\<id>，一并扫描
        if (Directory.Exists(ugc))
        {
            foreach (var dir in Directory.GetDirectories(ugc))
            {
                string id = Path.GetFileName(dir);
                if (seen.Add(id)) list.Add(Make(id, overrides, dir));
            }
        }
        return list;
    }

    private InstalledMod Make(string id, Dictionary<string, OverrideEntry> overrides, string folder)
    {
        string modinfo = Path.Combine(folder, "modinfo.lua");
        string name = File.Exists(modinfo) ? ModInfoParser.ParseFile(modinfo).Name : "（未下载，启动服务器时自动获取）";
        return new InstalledMod
        {
            Id = id,
            Name = name,
            Enabled = overrides.TryGetValue(id, out var e) && e.Enabled,
            Folder = folder,
        };
    }

    public async Task DownloadAsync(string id, IProgress<string> progress, CancellationToken ct)
    {
        int code = await SteamCmdService.DownloadWorkshopAsync(_steamCmdDir, id, progress, ct);
        if (code != 0)
        {
            progress.Report($"下载失败（steamcmd 退出码 {code}）");
            return;
        }

        string src = AppPaths.WorkshopContentDir(_steamCmdDir, id);
        if (!Directory.Exists(src))
        {
            progress.Report($"工坊目录未找到: {src}");
            return;
        }

        // 创意工坊 v1 打包（legacy.bin）：只要存在就自动解压还原 v2 目录结构。
        // 不依赖 modinfo 是否存在 —— 重复检查更新时也会用最新内容覆盖旧文件。
        if (Directory.GetFiles(src, "*.bin", SearchOption.AllDirectories).Length > 0)
        {
            UnpackLegacy(src, progress);
        }
        NormalizeLayout(src);

        string dst = Path.Combine(_modsDir, "workshop-" + id);
        progress.Report("正在复制到服务器 mods 目录...");
        await Task.Run(() =>
        {
            if (Directory.Exists(dst)) Directory.Delete(dst, true);
            CopyDirectory(src, dst);
        }, ct);

        var ids = LuaFileIo.ParseModSetupLua(SetupLuaPath);
        if (!ids.Contains(id)) ids.Add(id);
        ConfigWriter.WriteModSetupLua(_modsDir, ids);
        progress.Report("mod 下载并安装完成");
    }

    /// <summary>
    /// legacy.bin 是创意工坊 v1 打包格式，实际就是一个标准 ZIP。
    /// 把它解压到工坊目录，还原成 v2 的散文件布局。
    /// </summary>
    private static void UnpackLegacy(string src, IProgress<string> progress)
    {
        foreach (var bin in Directory.GetFiles(src, "*.bin", SearchOption.AllDirectories))
        {
            progress.Report($"检测到 legacy.bin（{Path.GetFileName(bin)}），开始解压转换...");
            try
            {
                using var fs = new FileStream(bin, FileMode.Open, FileAccess.Read, FileShare.Read);
                var head = new byte[2];
                fs.ReadExactly(head, 0, 2);
                if (head[0] != (byte)'P' || head[1] != (byte)'K')
                {
                    progress.Report("legacy.bin 不是 ZIP 格式，跳过");
                    continue;
                }
                fs.Position = 0;
                using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
                int n = 0;
                foreach (var e in zip.Entries)
                {
                    string rel = e.FullName.Replace('\\', '/');
                    if (string.IsNullOrEmpty(rel) || rel.EndsWith('/')) continue;
                    if (rel.Contains("..")) continue; // zip-slip 防护
                    string target = Path.Combine(src, rel.Replace('/', Path.DirectorySeparatorChar));
                    string? dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    using var es = e.Open();
                    using var os = File.Create(target);
                    es.CopyTo(os);
                    n++;
                }
                progress.Report($"legacy.bin 解压转换完成（{n} 个文件）");
                // 删除原 bin（steamcmd 可能还握着句柄，失败也无妨）
                try
                {
                    fs.Dispose();
                    File.Delete(bin);
                }
                catch
                {
                    progress.Report("原 bin 文件暂时被占用，保留（不影响使用）");
                }
            }
            catch (Exception ex)
            {
                progress.Report("legacy.bin 解压失败: " + ex.Message);
            }
        }
    }

    /// <summary>若解压后 modinfo.lua 被包在单层子目录里，上提一层。</summary>
    private static void NormalizeLayout(string dir)
    {
        if (File.Exists(Path.Combine(dir, "modinfo.lua"))) return;
        foreach (var sd in Directory.GetDirectories(dir))
        {
            if (!File.Exists(Path.Combine(sd, "modinfo.lua"))) continue;
            foreach (var f in Directory.GetFiles(sd, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(sd, f);
                string target = Path.Combine(dir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(f, target, true);
            }
            try { Directory.Delete(sd, true); } catch { }
            return;
        }
    }

    public void Delete(string id)
    {
        string dst = Path.Combine(_modsDir, "workshop-" + id);
        try { if (Directory.Exists(dst)) Directory.Delete(dst, true); } catch { }

        // 服务器运行后会把 mod 迁到 ugc_mods，删除时一并清掉
        string ugcRoot = Path.Combine(AppPaths.ServerDir(_steamCmdDir), "ugc_mods",
            Path.GetFileName(_clusterDir));
        foreach (var shard in new[] { "Master", "Caves" })
        {
            string ugc = Path.Combine(ugcRoot, shard, "content", "322330", id);
            try { if (Directory.Exists(ugc)) Directory.Delete(ugc, true); } catch { }
        }

        var ids = LuaFileIo.ParseModSetupLua(SetupLuaPath);
        if (ids.Remove(id)) ConfigWriter.WriteModSetupLua(_modsDir, ids);

        RemoveFromOverrides(MasterDir, id);
        RemoveFromOverrides(CavesDir, id);
    }

    private static void RemoveFromOverrides(string shardDir, string id)
    {
        string p = Path.Combine(shardDir, "modoverrides.lua");
        var overrides = LuaFileIo.ParseModOverrides(p);
        if (overrides.Remove(id)) ConfigWriter.WriteModOverrides(shardDir, overrides);
    }

    public Dictionary<string, OverrideEntry> LoadOverrides() =>
        LuaFileIo.ParseModOverrides(Path.Combine(MasterDir, "modoverrides.lua"));

    public void SaveOverrides(string shardDir, Dictionary<string, OverrideEntry> mods) =>
        ConfigWriter.WriteModOverrides(shardDir, mods);

    public void SetEnabled(string id, bool enabled)
    {
        foreach (var shard in new[] { MasterDir, CavesDir })
        {
            string p = Path.Combine(shard, "modoverrides.lua");
            var overrides = LuaFileIo.ParseModOverrides(p);
            if (!overrides.TryGetValue(id, out var e))
            {
                e = new OverrideEntry();
                overrides[id] = e;
            }
            e.Enabled = enabled;
            ConfigWriter.WriteModOverrides(shard, overrides);
        }
    }

    private static void CopyDirectory(string src, string dst)
    {
        if (!Directory.Exists(src)) return;
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, file);
            if (rel.EndsWith("_legacy.bin", StringComparison.OrdinalIgnoreCase)) continue; // 残留的 v1 打包壳
            string target = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
}
