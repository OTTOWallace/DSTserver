using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using DSTserver.Models;

namespace DSTserver.Services;

/// <summary>
/// 启动 / 停止 DST 专用服务器进程（Master + Caves 两个分片），
/// 支持向服务器控制台（stdin）发送指令、从输出解析玩家列表。
/// </summary>
public class ServerController
{
    private readonly List<Process> _procs = new();
    private readonly Dictionary<string, PlayerInfo> _pendingPlayers = new();
    private bool _inPlayerList;
    private bool _tokenConflictNotified;

    public event Action<string>? Log;
    public event Action<List<PlayerInfo>>? PlayerListUpdated;

    /// <summary>大厅注册报 E_ROWID_EXIST（服务器令牌被占用）时触发，每次开服只触发一次。</summary>
    public event Action? LobbyTokenConflict;

    public bool IsRunning
    {
        get { lock (_procs) { return _procs.Exists(p => !p.HasExited); } }
    }

    public void Start(string serverExe, string clusterName, bool caves)
    {
        Stop();
        _tokenConflictNotified = false;
        if (!File.Exists(serverExe))
        {
            Log?.Invoke($"找不到服务器程序: {serverExe}");
            return;
        }
        string workDir = Path.GetDirectoryName(serverExe)!;
        var shards = caves ? new[] { "Master", "Caves" } : new[] { "Master" };
        foreach (var shard in shards)
        {
            var psi = new ProcessStartInfo
            {
                FileName = serverExe,
                Arguments = $"-cluster {clusterName} -shard {shard}",
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                StandardInputEncoding = new UTF8Encoding(false),
            };
            var proc = new Process { StartInfo = psi };
            string tag = shard;
            proc.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                if (HandlePlayerListLine(e.Data)) return;
                HandleLobbyLine(e.Data);
                Log?.Invoke($"[{tag}] {e.Data}");
            };
            proc.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                if (HandlePlayerListLine(e.Data)) return;
                HandleLobbyLine(e.Data);
                Log?.Invoke($"[{tag}] {e.Data}");
            };
            try
            {
                if (proc.Start())
                {
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    lock (_procs) _procs.Add(proc);
                    Log?.Invoke($"{shard} 分片已启动 (PID {proc.Id})");
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"启动 {shard} 失败: {ex.Message}");
            }
        }
    }

    /// <summary>向服务器控制台发送指令（写入第一个存活分片的 stdin）。</summary>
    public bool SendCommand(string command)
    {
        lock (_procs)
        {
            foreach (var p in _procs)
            {
                if (p.HasExited) continue;
                try
                {
                    p.StandardInput.WriteLine(command);
                    p.StandardInput.Flush();
                    return true;
                }
                catch (Exception ex)
                {
                    Log?.Invoke("控制台指令发送失败: " + ex.Message);
                    return false;
                }
            }
        }
        Log?.Invoke("服务器未运行，指令未发送");
        return false;
    }

    /// <summary>查询在线玩家（用固定标记从控制台输出解析）。</summary>
    public const string PlayerListCommand =
        "print(\"PLAYERLIST_BEGIN\") " +
        "for k,v in pairs(AllPlayers) do " +
        "print(\"PLAYERLIST|\"..tostring(v.userid)..\"|\"..tostring(v.name)..\"|\"..tostring(v.NetID)) end " +
        "print(\"PLAYERLIST_END\")";

    /// <summary>给指定玩家发放物品的 Lua 指令。</summary>
    public static string BuildGiveCommand(string userId, string prefab, int count)
    {
        string id = userId.Replace("\"", "").Replace("\\", "");
        string p = prefab.Replace("\"", "").Replace("\\", "");
        return
            $"local pid=\"{id}\" local item=\"{p}\" local n={count} " +
            "for k,v in pairs(AllPlayers) do if tostring(v.userid)==pid then " +
            "local inv=v.components and v.components.inventory if inv then " +
            "for i=1,n do inv:GiveItem(SpawnPrefab(item)) end end break end end";
    }

    /// <summary>检测大厅令牌冲突（E_ROWID_EXIST），每次开服只通知一次。</summary>
    private void HandleLobbyLine(string line)
    {
        if (!line.Contains("E_ROWID_EXIST") || _tokenConflictNotified) return;
        _tokenConflictNotified = true;
        LobbyTokenConflict?.Invoke();
    }

    private bool HandlePlayerListLine(string line)
    {
        if (line.Contains("PLAYERLIST_BEGIN"))
        {
            _pendingPlayers.Clear();
            _inPlayerList = true;
            return true;
        }
        if (_inPlayerList && line.Contains("PLAYERLIST_END"))
        {
            _inPlayerList = false;
            PlayerListUpdated?.Invoke(new List<PlayerInfo>(_pendingPlayers.Values));
            return true;
        }
        if (_inPlayerList && line.Contains("PLAYERLIST|"))
        {
            var parts = line.Split('|');
            if (parts.Length >= 3)
            {
                var p = new PlayerInfo
                {
                    UserId = parts[1].Trim(),
                    Name = parts[2].Trim(),
                    NetId = parts.Length >= 4 ? parts[3].Trim() : "",
                };
                if (p.UserId.Length > 0) _pendingPlayers[p.UserId] = p;
            }
            return true;
        }
        return false;
    }

    public void Stop()
    {
        lock (_procs)
        {
            foreach (var p in _procs)
            {
                try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
                try { p.Dispose(); } catch { }
            }
            _procs.Clear();
        }
        _pendingPlayers.Clear();
        _inPlayerList = false;
        _tokenConflictNotified = false;
    }
}
