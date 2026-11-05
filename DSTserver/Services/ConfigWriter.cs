using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DSTserver.Models;

namespace DSTserver.Services;

/// <summary>写出 DST 服务器的各类配置文件。</summary>
public static class ConfigWriter
{
    public static void WriteClusterIni(string clusterDir, ClusterSettings s)
    {
        Directory.CreateDirectory(clusterDir);
        var sb = new StringBuilder();
        sb.AppendLine("[GAMEPLAY]");
        sb.AppendLine($"game_mode = {s.GameMode}");
        sb.AppendLine($"max_players = {s.MaxPlayers}");
        sb.AppendLine($"pvp = {(s.Pvp ? "true" : "false")}");
        sb.AppendLine($"pause_when_empty = {(s.PauseWhenEmpty ? "true" : "false")}");
        sb.AppendLine("[NETWORK]");
        sb.AppendLine($"cluster_name = {s.ClusterName}");
        sb.AppendLine($"cluster_description = {s.Description}");
        sb.AppendLine($"cluster_password = {s.Password}");
        sb.AppendLine("cluster_intention = cooperative");
        sb.AppendLine("[MISC]");
        sb.AppendLine("console_enabled = true");
        sb.AppendLine("[SHARD]");
        sb.AppendLine($"shard_enabled = {(s.EnableCaves ? "true" : "false")}");
        sb.AppendLine("bind_ip = 127.0.0.1");
        sb.AppendLine("master_ip = 127.0.0.1");
        sb.AppendLine("master_port = 10888");
        sb.AppendLine("cluster_key = DSTserverKey");
        File.WriteAllText(Path.Combine(clusterDir, "cluster.ini"), sb.ToString(), new UTF8Encoding(false));
    }

    public static void WriteServerIni(string shardDir, bool isMaster)
    {
        Directory.CreateDirectory(shardDir);
        var sb = new StringBuilder();
        sb.AppendLine("[NETWORK]");
        sb.AppendLine(isMaster ? "server_port = 10999" : "server_port = 11000");
        sb.AppendLine("[SHARD]");
        sb.AppendLine($"is_master = {(isMaster ? "true" : "false")}");
        sb.AppendLine(isMaster ? "name = SNDefaultMaster" : "name = SNDefaultCaves");
        if (!isMaster) sb.AppendLine("id = 232826026");
        sb.AppendLine("[STEAM]");
        sb.AppendLine(isMaster ? "master_server_port = 27018" : "master_server_port = 27019");
        sb.AppendLine(isMaster ? "authentication_port = 8768" : "authentication_port = 8769");
        sb.AppendLine("[ACCOUNT]");
        sb.AppendLine("encode_user_path = true");
        File.WriteAllText(Path.Combine(shardDir, "server.ini"), sb.ToString(), new UTF8Encoding(false));
    }

    public static void WriteToken(string clusterDir, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        File.WriteAllText(Path.Combine(clusterDir, "cluster_token.txt"), token.Trim(), new UTF8Encoding(false));
    }

    public static void WriteModSetupLua(string modsDir, IEnumerable<string> ids)
    {
        Directory.CreateDirectory(modsDir);
        var sb = new StringBuilder();
        foreach (var id in ids) sb.AppendLine($"ServerModSetup(\"{id}\")");
        File.WriteAllText(Path.Combine(modsDir, "dedicated_server_mods_setup.lua"), sb.ToString(), new UTF8Encoding(false));
    }

    public static string LuaString(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public static string LuaValue(object? v)
    {
        switch (v)
        {
            case null: return "nil";
            case bool b: return b ? "true" : "false";
            case string str: return LuaString(str);
            case double d: return d.ToString(CultureInfo.InvariantCulture);
            case float f: return f.ToString(CultureInfo.InvariantCulture);
            case int i2: return i2.ToString(CultureInfo.InvariantCulture);
            case long l2: return l2.ToString(CultureInfo.InvariantCulture);
            default: return LuaString(v.ToString() ?? "");
        }
    }

    public static void WriteModOverrides(string shardDir, Dictionary<string, OverrideEntry> mods)
    {
        Directory.CreateDirectory(shardDir);
        var sb = new StringBuilder();
        sb.AppendLine("return {");
        foreach (var kv in mods)
        {
            sb.AppendLine($"\t[{LuaString(kv.Key)}] = {{");
            sb.AppendLine($"\t\tenabled = {(kv.Value.Enabled ? "true" : "false")},");
            if (kv.Value.Options.Count > 0)
            {
                sb.AppendLine("\t\tconfiguration_options = {");
                foreach (var o in kv.Value.Options)
                    sb.AppendLine($"\t\t\t[{LuaString(o.Key)}] = {LuaValue(o.Value)},");
                sb.AppendLine("\t\t},");
            }
            sb.AppendLine("\t},");
        }
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(shardDir, "modoverrides.lua"), sb.ToString(), new UTF8Encoding(false));
    }

    public static void WriteWorldgenOverride(string shardDir, Dictionary<string, object?> overrides)
    {
        Directory.CreateDirectory(shardDir);
        var sb = new StringBuilder();
        sb.AppendLine("return {");
        sb.AppendLine("\toverride_enabled = true,");
        sb.AppendLine("\toverrides = {");
        foreach (var kv in overrides)
            sb.AppendLine($"\t\t{kv.Key} = {LuaValue(kv.Value)},");
        sb.AppendLine("\t},");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(shardDir, "worldgenoverride.lua"), sb.ToString(), new UTF8Encoding(false));
    }
}
