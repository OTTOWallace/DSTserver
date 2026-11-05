using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSTserver.Models;
using DSTserver.Services;

if (args.Length > 0)
{
    switch (args[0])
    {
        case "--genconfig": return GenConfig();
        case "--test-mod": return await TestMod(args.Length > 1 ? args[1] : "378160973");
        case "--del-mod": return DelMod(args.Length > 1 ? args[1] : "");
        default:
            Console.WriteLine("unknown arg: " + args[0]);
            return 1;
    }
}

string tmp = Path.Combine(Path.GetTempPath(), "dsh_mod_test");
Directory.CreateDirectory(tmp);
File.WriteAllText(Path.Combine(tmp, "modinfo.lua"), SampleModInfo());

int failures = 0;
void Check(bool cond, string name)
{
    Console.WriteLine((cond ? "PASS  " : "FAIL  ") + name);
    if (!cond) failures++;
}

// ---- 1. modinfo.lua 解析 ----
var meta = ModInfoParser.ParseFile(Path.Combine(tmp, "modinfo.lua"));
Check(meta.Name == "Test Mod", $"mod name 解析（得到 '{meta.Name}'）");
Check(meta.Options.Count == 3, $"配置项数量=3（实际 {meta.Options.Count}）");
if (meta.Options.Count == 3)
{
    var o1 = meta.Options[0];
    Check(o1.Name == "SHOW_HEALTH" && o1.Label == "显示血量", "选项1 name/label（含中文）");
    Check(o1.Options.Count == 2 && Equals(o1.Options[1].Data, 1.0), "选项1 options data");
    Check(Equals(o1.Default, 1.0), "选项1 default");
    var o2 = meta.Options[1];
    Check(o2.Options.Count == 3 && Equals(o2.Options[2].Data, 1.2), "浮点 data 解析");
    var o3 = meta.Options[2];
    Check(o3.Default is bool && (bool)o3.Default, "布尔 default 解析");
}

// ---- 2. modoverrides.lua 写入→读回 ----
var overrides = new Dictionary<string, OverrideEntry>
{
    ["378160973"] = new OverrideEntry
    {
        Enabled = true,
        Options = new Dictionary<string, object?>
        {
            ["SHOW_HEALTH"] = 1L,
            ["SCALE"] = 1.2,
        },
    },
    ["999"] = new OverrideEntry { Enabled = false },
};
string shardDir = Path.Combine(tmp, "Master");
ConfigWriter.WriteModOverrides(shardDir, overrides);
Console.WriteLine("---- 生成的 modoverrides.lua ----");
Console.WriteLine(File.ReadAllText(Path.Combine(shardDir, "modoverrides.lua")));
var back = LuaFileIo.ParseModOverrides(Path.Combine(shardDir, "modoverrides.lua"));
Check(back.ContainsKey("378160973") && back["378160973"].Enabled, "overrides 键与启用状态");
Check(Equals(back["378160973"].Options["SHOW_HEALTH"], 1.0), "overrides 整数值回读");
Check(Equals(back["378160973"].Options["SCALE"], 1.2), "overrides 浮点值回读");
Check(back.ContainsKey("999") && !back["999"].Enabled, "禁用项回读");

// ---- 3. worldgenoverride.lua 写入→读回 ----
var wg = new Dictionary<string, object?> { ["world_size"] = "huge", ["season"] = "autumn" };
ConfigWriter.WriteWorldgenOverride(shardDir, wg);
Console.WriteLine("---- 生成的 worldgenoverride.lua ----");
Console.WriteLine(File.ReadAllText(Path.Combine(shardDir, "worldgenoverride.lua")));
var wgBack = LuaFileIo.ParseWorldgenOverrides(Path.Combine(shardDir, "worldgenoverride.lua"));
Check(Equals(wgBack["world_size"], "huge"), "worldgen 字符串回读");

// ---- 4. dedicated_server_mods_setup.lua 写入→读回 ----
ConfigWriter.WriteModSetupLua(Path.Combine(tmp, "mods"), new[] { "378160973", "666" });
var ids = LuaFileIo.ParseModSetupLua(Path.Combine(tmp, "mods", "dedicated_server_mods_setup.lua"));
Check(ids.Count == 2 && ids[1] == "666", "setup lua 回读");

// ---- 5. 块注释与行注释 ----
string tricky = "-- 行注释 name = \"假\"\n--[[ 块注释\nconfiguration_options = {}\n]]\nname = \"真\"";
Check((LuaMini.FindAssignment(tricky, "name") as string) == "真", "注释剥离后取值");

// ---- 6. 给物品指令构造与注入防护 ----
string give = ServerController.BuildGiveCommand("KU_abc123", "goldnugget", 42);
Check(give.Contains("\"KU_abc123\"") && give.Contains("goldnugget") && give.Contains("n=42"), "给物品指令模板");
string evil = ServerController.BuildGiveCommand("KU_x\" print(\"pwned\")", "log", 1);
Check(!evil.Contains("print(\"pwned\")"), "指令注入防护（引号剥离，载荷失效）");

// ---- 7. 默认设置 ----
var def = new AppSettings();
Check(def.Cluster.ClusterName == "找啊找啊找饥友", "默认服务器名");
Check(def.Cluster.Token.Length == 0, "默认令牌留空（防泄露，真实令牌存本机 settings.json）");
Check(def.BackupIntervalHours == 24 && def.ModCheckIntervalHours == 24, "默认定时参数 24h");

Console.WriteLine(failures == 0 ? "== ALL PASS ==" : $"== {failures} FAILED ==");
return failures == 0 ? 0 : 1;

// ---- 生成真实集群配置 ----
// ---- 删除 mod（验证 mods + ugc_mods 两处都会被清）----
static int DelMod(string id)
{
    if (string.IsNullOrEmpty(id)) { Console.WriteLine("usage: --del-mod <id>"); return 1; }
    const string steamDir = @"C:\VSPJ\DSTserver\bin\Debug\net10.0-windows\runtime\steamcmd";
    var mods = new ModService(steamDir, AppPaths.ClusterDir(1));
    mods.Delete(id);
    Console.WriteLine("已删除 " + id);
    return 0;
}

static int GenConfig()
{
    string cd = AppPaths.ClusterDir(1);
    var s = new ClusterSettings
    {
        ClusterName = "DST 测试服",
        Description = "DSTserver 生成",
        Password = "",
        GameMode = "survival",
        MaxPlayers = 6,
        Pvp = false,
        PauseWhenEmpty = true,
        EnableCaves = true,
    };
    ConfigWriter.WriteClusterIni(cd, s);
    ConfigWriter.WriteServerIni(Path.Combine(cd, "Master"), true);
    ConfigWriter.WriteServerIni(Path.Combine(cd, "Caves"), false);
    ConfigWriter.WriteToken(cd, "placeholder-token");
    ConfigWriter.WriteWorldgenOverride(Path.Combine(cd, "Master"), new Dictionary<string, object?> { ["world_size"] = "huge", ["season_start"] = "autumn" });
    ConfigWriter.WriteWorldgenOverride(Path.Combine(cd, "Caves"), new Dictionary<string, object?> { ["world_size"] = "huge" });

    Console.WriteLine("已写入集群配置: " + cd);
    foreach (var f in Directory.GetFiles(cd, "*", SearchOption.AllDirectories))
        Console.WriteLine("  " + Path.GetRelativePath(cd, f));
    return 0;
}

// ---- 真实工坊 mod 全链路 ----
static async Task<int> TestMod(string id)
{
    string steamDir = @"C:\VSPJ\DSTserver\bin\Debug\net10.0-windows\runtime\steamcmd";
    string clusterDir = AppPaths.ClusterDir(1);
    var mods = new ModService(steamDir, clusterDir);
    var progress = new Progress<string>(s => Console.WriteLine("  " + s));

    Console.WriteLine($"开始下载 mod {id} ...");
    await mods.DownloadAsync(id, progress, CancellationToken.None);

    var list = mods.ListInstalled();
    Console.WriteLine($"已安装列表（{list.Count}）:");
    foreach (var m in list) Console.WriteLine($"  {m.Id} '{m.Name}' enabled={m.Enabled}");

    string modinfo = Path.Combine(mods.ModsDir, "workshop-" + id, "modinfo.lua");
    if (!File.Exists(modinfo)) { Console.WriteLine("modinfo.lua 未找到"); return 1; }

    var meta = ModInfoParser.ParseFile(modinfo);
    Console.WriteLine($"名称: {meta.Name}  配置项: {meta.Options.Count}");
    foreach (var o in meta.Options)
    {
        Console.WriteLine($"  - [{o.Name}] {o.Label}  default={o.Default}  选项数={o.Options.Count}");
        foreach (var oi in o.Options.Take(4))
            Console.WriteLine($"      * {oi.Description} -> {oi.Data}");
        if (o.Options.Count > 4) Console.WriteLine($"      ... 共 {o.Options.Count} 项");
    }

    // 写默认配置并回读
    var overrides = mods.LoadOverrides();
    var entry = new OverrideEntry { Enabled = true };
    foreach (var o in meta.Options) entry.Options[o.Name] = o.Default;
    overrides[id] = entry;
    mods.SaveOverrides(Path.Combine(clusterDir, "Master"), overrides);
    mods.SaveOverrides(Path.Combine(clusterDir, "Caves"), overrides);
    Console.WriteLine("已写入 modoverrides.lua（Master + Caves）");

    var back = LuaFileIo.ParseModOverrides(Path.Combine(clusterDir, "Master", "modoverrides.lua"));
    bool ok = back.TryGetValue(id, out var be) && be.Enabled && be.Options.Count == meta.Options.Count;
    Console.WriteLine($"回读验证: {(ok ? "PASS" : "FAIL")}（启用={back.TryGetValue(id, out var be2) && be2.Enabled}，选项数={(back.TryGetValue(id, out var be3) ? be3.Options.Count : 0)}/{meta.Options.Count}）");
    return ok ? 0 : 1;
}

static string SampleModInfo() => @"
-- 测试用 modinfo.lua
name = ""Test Mod""
description = ""parser test""
author = ""dsh""
version = ""1.0""
dst_compatible = true
all_clients_require_mod = true
client_only_mod = false
server_only_mod = true
api_version = 10

configuration_options =
{
    {
        name = ""SHOW_HEALTH"",
        label = ""显示血量"",
        hover = ""在生物头顶显示血量数字"",
        options =
        {
            {description = ""关闭"", data = 0, hover = """"},
            {description = ""开启"", data = 1},
        },
        default = 1,
    },
    {
        name = ""SCALE"",
        label = ""缩放比例"",
        options =
        {
            {description = ""小"", data = 0.8},
            {description = ""中"", data = 1.0},
            {description = ""大"", data = 1.2},
        },
        default = 1.0,
    },
    {
        name = ""ENABLE_EXTRA"",
        label = ""附加功能"",
        default = true,
    },
}
";
