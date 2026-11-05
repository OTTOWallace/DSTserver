using System;
using System.Collections.Generic;

namespace DSTserver.Models;

/// <summary>选项列表中的一个条目（description 显示，data 写入 lua）。</summary>
public class OptionItem
{
    public string Description { get; set; } = "";
    public object? Data { get; set; }
    public override string ToString() => Description;
}

/// <summary>modinfo.lua configuration_options 里的一项配置。</summary>
public class ModOption
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string Hover { get; set; } = "";
    public object? Default { get; set; }
    public List<OptionItem> Options { get; set; } = new();
}

/// <summary>一个 mod 的元数据。</summary>
public class ModMeta
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<ModOption> Options { get; set; } = new();
}

/// <summary>已安装 mod 列表项。</summary>
public class InstalledMod : System.ComponentModel.INotifyPropertyChanged
{
    private bool _enabled;
    private bool _selected;

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>是否在服务器上启用（写入 modoverrides）。</summary>
    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Enabled))); }
    }

    /// <summary>列表里的勾选状态（用于批量删除，不持久化）。</summary>
    public bool Selected
    {
        get => _selected;
        set { _selected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Selected))); }
    }

    public string Folder { get; set; } = "";

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>modoverrides.lua 中单个 mod 的条目。</summary>
public class OverrideEntry
{
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object?> Options { get; set; } = new();
}

/// <summary>世界生成变量定义。</summary>
public class WorldSettingDef
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public List<OptionItem> Options { get; set; } = new();
}

/// <summary>集群设置。</summary>
public class ClusterSettings
{
    public string ClusterName { get; set; } = "找啊找啊找饥友";
    public string Description { get; set; } = "";
    public string Password { get; set; } = "";
    public string GameMode { get; set; } = "survival";
    public int MaxPlayers { get; set; } = 6;
    public bool Pvp { get; set; }
    public bool PauseWhenEmpty { get; set; } = true;
    public bool EnableCaves { get; set; } = true;
    public int ClusterSlot { get; set; } = 1;
    public string Token { get; set; } = ""; // 默认留空；真实令牌存本机 settings.json（不入库）
}

/// <summary>服务器内玩家信息。</summary>
public class PlayerInfo
{
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string NetId { get; set; } = "";
    public override string ToString() => $"{Name}（{UserId}）";
}

/// <summary>一次存档备份。</summary>
public class BackupInfo
{
    public string Path { get; set; } = "";
    public DateTime Time { get; set; }
    public string Slot { get; set; } = "";
    public string Display => Time.ToString("yyyy-MM-dd HH:mm:ss");
    public override string ToString() => Display;
}

/// <summary>快捷指令的物品条目。</summary>
public class GiveItem
{
    public string Prefab { get; set; } = "";
    public string Label { get; set; } = "";
    public override string ToString() => Label;
}
