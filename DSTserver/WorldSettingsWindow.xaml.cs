using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DSTserver.Models;
using DSTserver.Services;

namespace DSTserver;

public partial class WorldSettingsWindow : Window
{
    private readonly string _clusterDir;
    private readonly List<(WorldSettingDef Def, ComboBox Cmb)> _controls = new();
    private readonly Dictionary<string, object?> _current;

    private static readonly (string Desc, string Data)[] SeasonLen = new[]
    {
        ("默认", "default"),
        ("无此季节", "noseason"),
        ("极短", "veryshortseason"),
        ("短", "shortseason"),
        ("长", "longseason"),
        ("极长", "verylongseason"),
        ("随机", "random"),
    };

    private static readonly (string Key, string Label, (string Desc, string Data)[] Options)[] Defs = new[]
    {
        ("world_size", "世界大小", new[] { ("默认", "default"), ("中", "medium"), ("大", "large"), ("巨大", "huge") }),
        ("season", "季节循环", new[] { ("默认", "default"), ("秋", "autumn"), ("冬", "winter"), ("春", "spring"), ("夏", "summer") }),
        ("season_start", "起始季节", new[] { ("默认", "default"), ("秋", "autumn"), ("冬", "winter"), ("春", "spring"), ("夏", "summer") }),
        ("day", "昼夜节奏", new[] { ("默认", "default"), ("长白天", "longday"), ("长黄昏", "longdusk"), ("长夜晚", "longnight"), ("无", "none") }),
        ("branching", "世界分支", new[] { ("默认", "default"), ("最少", "least"), ("随机", "random"), ("最多", "most") }),
        ("loop", "环形连接", new[] { ("默认", "default"), ("从不", "never"), ("总是", "always") }),
        ("autumn", "秋天长短", SeasonLen),
        ("winter", "冬天长短", SeasonLen),
        ("spring", "春天长短", SeasonLen),
        ("summer", "夏天长短", SeasonLen),
        ("disease", "疾病", new[] { ("默认", "default"), ("无", "none"), ("从不", "never"), ("随机", "random"), ("总是", "always") }),
        ("decay", "腐烂速度", new[] { ("默认", "default"), ("无", "none"), ("慢", "slow"), ("快", "fast") }),
        ("boons", "惊喜箱", new[] { ("默认", "default"), ("从不", "never"), ("总是", "always"), ("随机", "random") }),
        ("wildfires", "野火", new[] { ("默认", "default"), ("从不", "never"), ("总是", "always"), ("随机", "random") }),
        ("frograin", "青蛙雨", new[] { ("默认", "default"), ("从不", "never"), ("总是", "always"), ("随机", "random") }),
        ("hounds", "猎犬袭击", new[] { ("默认", "default"), ("从不", "never"), ("总是", "always"), ("随机", "random") }),
        ("spiders", "蜘蛛", new[] { ("默认", "default"), ("从不", "never"), ("总是", "always"), ("随机", "random") }),
        ("regrowth", "资源再生", new[] { ("默认", "default"), ("从不", "never"), ("总是", "always") }),
        ("touchstone", "复活祭坛", new[] { ("默认", "default"), ("从不", "never"), ("总是", "always"), ("随机", "random") }),
        ("prefabswaps_start", "起始资源变体", new[] { ("默认", "default"), ("无", "none"), ("经典", "classic"), ("高度随机", "highly_random") }),
    };

    public WorldSettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _clusterDir = AppPaths.ClusterDir(settings.Cluster.ClusterSlot);
        _current = LuaFileIo.ParseWorldgenOverrides(Path.Combine(_clusterDir, "Master", "worldgenoverride.lua"));

        foreach (var (key, label, opts) in Defs)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var cmb = new ComboBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            int sel = 0;
            object? cur = _current.TryGetValue(key, out var cv) ? cv : "default";
            for (int i = 0; i < opts.Length; i++)
            {
                cmb.Items.Add(new OptionItem { Description = opts[i].Desc, Data = opts[i].Data });
                if (Equals(cur, opts[i].Data)) sel = i;
            }
            cmb.SelectedIndex = sel;
            Grid.SetColumn(cmb, 1);
            grid.Children.Add(cmb);

            _controls.Add((new WorldSettingDef { Key = key, Label = label }, cmb));
            PnlWorld.Children.Add(grid);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var overrides = new Dictionary<string, object?>();
        foreach (var (def, cmb) in _controls)
        {
            if (cmb.SelectedItem is OptionItem oi && oi.Data is string s && s != "default")
                overrides[def.Key] = s;
        }

        ConfigWriter.WriteWorldgenOverride(Path.Combine(_clusterDir, "Master"), overrides);
        bool applyCaves = ChkApplyCaves.IsChecked == true;
        if (applyCaves)
            ConfigWriter.WriteWorldgenOverride(Path.Combine(_clusterDir, "Caves"), overrides);

        TxtHint.Text = $"已保存 {overrides.Count} 项到 worldgenoverride.lua（{(applyCaves ? "Master + Caves" : "仅 Master")}）";
    }
}
