using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DSTserver.Models;

namespace DSTserver.Services;

/// <summary>解析 modinfo.lua。</summary>
public static class ModInfoParser
{
    public static ModMeta ParseFile(string modinfoPath)
    {
        var meta = new ModMeta();
        try
        {
            string text = LuaMini.StripComments(File.ReadAllText(modinfoPath));

            meta.Name = (LuaMini.FindAssignment(text, "name") as string) ?? "未知";

            if (LuaMini.FindAssignment(text, "configuration_options") is Dictionary<string, object?> table)
            {
                foreach (var kv in table)
                {
                    if (kv.Value is not Dictionary<string, object?> e) continue;
                    var opt = new ModOption
                    {
                        Name = e.TryGetValue("name", out var n) ? n as string ?? "" : "",
                        Label = e.TryGetValue("label", out var l) ? l as string ?? "" : "",
                        Hover = e.TryGetValue("hover", out var h) ? h as string ?? "" : "",
                        Default = e.TryGetValue("default", out var d) ? d : null,
                    };
                    if (e.TryGetValue("options", out var o) && o is Dictionary<string, object?> opts)
                    {
                        foreach (var ov in opts)
                        {
                            if (ov.Value is not Dictionary<string, object?> oe) continue;
                            opt.Options.Add(new OptionItem
                            {
                                Description = oe.TryGetValue("description", out var dd) ? dd as string ?? "" : "",
                                Data = oe.TryGetValue("data", out var dta) ? dta : null,
                            });
                        }
                    }
                    if (opt.Name.Length > 0) meta.Options.Add(opt);
                }
            }
        }
        catch { }
        return meta;
    }
}

/// <summary>读取 / 解析各类 lua 配置文件。</summary>
public static class LuaFileIo
{
    public static Dictionary<string, OverrideEntry> ParseModOverrides(string luaPath)
    {
        var result = new Dictionary<string, OverrideEntry>();
        try
        {
            if (!File.Exists(luaPath)) return result;
            string text = LuaMini.StripComments(File.ReadAllText(luaPath));
            int i = text.IndexOf("return", StringComparison.Ordinal);
            if (i < 0) return result;
            i += "return".Length;
            if (LuaMini.ParseValue(text, ref i) is not Dictionary<string, object?> table) return result;

            foreach (var kv in table)
            {
                if (kv.Value is not Dictionary<string, object?> e) continue;
                var entry = new OverrideEntry
                {
                    Enabled = e.TryGetValue("enabled", out var en) ? en is not false : true,
                };
                if (e.TryGetValue("configuration_options", out var co) && co is Dictionary<string, object?> cod)
                {
                    foreach (var okv in cod) entry.Options[okv.Key] = okv.Value;
                }
                result[kv.Key] = entry;
            }
        }
        catch { }
        return result;
    }

    public static Dictionary<string, object?> ParseWorldgenOverrides(string luaPath)
    {
        var result = new Dictionary<string, object?>();
        try
        {
            if (!File.Exists(luaPath)) return result;
            string text = LuaMini.StripComments(File.ReadAllText(luaPath));
            int i = text.IndexOf("return", StringComparison.Ordinal);
            if (i < 0) return result;
            i += "return".Length;
            if (LuaMini.ParseValue(text, ref i) is Dictionary<string, object?> table &&
                table.TryGetValue("overrides", out var ov) && ov is Dictionary<string, object?> od)
            {
                foreach (var kv in od) result[kv.Key] = kv.Value;
            }
        }
        catch { }
        return result;
    }

    public static List<string> ParseModSetupLua(string path)
    {
        var ids = new List<string>();
        try
        {
            if (!File.Exists(path)) return ids;
            foreach (var line in File.ReadAllLines(path))
            {
                string t = line.Trim();
                if (t.StartsWith("--")) continue; // 跳过注释掉的示例行
                var m = Regex.Match(t, @"ServerModSetup\(\s*""(\d+)""");
                if (m.Success) ids.Add(m.Groups[1].Value);
            }
        }
        catch { }
        return ids;
    }
}
