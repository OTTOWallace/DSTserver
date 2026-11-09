using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace DSTserver.Services;

/// <summary>存档槽位信息（用于删除存档窗口）。</summary>
public class SlotInfo : INotifyPropertyChanged
{
    private bool _selected;

    public int Slot { get; set; }
    public string Name { get; set; } = "";
    public bool HasSave { get; set; }
    public long SaveBytes { get; set; }

    public string SizeText
    {
        get
        {
            if (SaveBytes <= 0) return HasSave ? "无存档数据" : "未创建";
            if (SaveBytes < 1024 * 1024) return $"{SaveBytes / 1024.0:F1} KB";
            if (SaveBytes < 1024L * 1024 * 1024) return $"{SaveBytes / 1024.0 / 1024:F1} MB";
            return $"{SaveBytes / 1024.0 / 1024 / 1024:F2} GB";
        }
    }

    public bool Selected
    {
        get => _selected;
        set { _selected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>存档（世界数据）的查询与删除。</summary>
public static class SaveService
{
    /// <summary>列出全部（20 个）槽位的存档信息。</summary>
    public static List<SlotInfo> ListSlots()
    {
        var list = new List<SlotInfo>();
        for (int slot = 1; slot <= 20; slot++)
        {
            string dir = AppPaths.ClusterDir(slot);
            var info = new SlotInfo { Slot = slot, Name = GetSlotName(slot) };

            long bytes = 0;
            bool hasSave = false;
            foreach (var shard in new[] { "Master", "Caves" })
            {
                string save = Path.Combine(dir, shard, "save");
                if (Directory.Exists(save))
                {
                    hasSave = true;
                    foreach (var f in Directory.GetFiles(save, "*", SearchOption.AllDirectories))
                    {
                        try { bytes += new FileInfo(f).Length; } catch { }
                    }
                }
            }
            info.HasSave = hasSave;
            info.SaveBytes = bytes;
            list.Add(info);
        }
        return list;
    }

    /// <summary>槽位的服务器名；未创建时返回“（未创建）”。</summary>
    public static string GetSlotName(int slot)
    {
        string dir = AppPaths.ClusterDir(slot);
        try
        {
            string ini = Path.Combine(dir, "cluster.ini");
            if (!File.Exists(ini)) return "（未创建）";
            foreach (var line in File.ReadAllLines(ini))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"cluster_name\s*=\s*(.+)");
                if (m.Success) return m.Groups[1].Value.Trim();
            }
        }
        catch { }
        return "（未创建）";
    }

    /// <summary>删除指定槽位的世界存档（Master/Caves 的 save 目录），保留服务器配置。</summary>
    public static void DeleteSaves(string clusterDir)
    {
        foreach (var shard in new[] { "Master", "Caves" })
        {
            string save = Path.Combine(clusterDir, shard, "save");
            try { if (Directory.Exists(save)) Directory.Delete(save, true); } catch { }
        }
    }
}
