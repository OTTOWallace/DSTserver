using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DSTserver.Models;
using DSTserver.Services;

namespace DSTserver;

public partial class ModManagerWindow : Window
{
    private readonly ModService _mods;
    private readonly string _clusterDir;
    private CancellationTokenSource? _cts;
    private InstalledMod? _selected;
    private readonly List<(ModOption Opt, FrameworkElement Ctl)> _optionControls = new();
    private bool _suppressSelectAll;

    public ModManagerWindow(AppSettings settings)
    {
        InitializeComponent();
        _clusterDir = AppPaths.ClusterDir(settings.Cluster.ClusterSlot);
        _mods = new ModService(AppPaths.SteamCmdDir(settings.SteamCmdDir), _clusterDir);
        RefreshList();
    }

    private void RefreshList()
    {
        var list = _mods.ListInstalled();
        LstMods.ItemsSource = null;
        LstMods.ItemsSource = list;
        _suppressSelectAll = true;
        ChkSelectAll.IsChecked = false;
        _suppressSelectAll = false;
        RefreshCheckedCount();
        TxtStatus.Text = $"共 {list.Count} 个 mod";
    }

    private IEnumerable<InstalledMod> CurrentMods =>
        LstMods.ItemsSource as IEnumerable<InstalledMod> ?? Enumerable.Empty<InstalledMod>();

    private List<InstalledMod> CheckedMods => CurrentMods.Where(m => m.Selected).ToList();

    private void RefreshCheckedCount()
    {
        TxtCheckedCount.Text = $"已勾选 {CheckedMods.Count} 个";
    }

    private void ChkSelectAll_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressSelectAll) return;
        bool on = ChkSelectAll.IsChecked == true;
        foreach (var m in CurrentMods) m.Selected = on;
        RefreshCheckedCount();
    }

    private void ModSelChanged(object sender, RoutedEventArgs e) => RefreshCheckedCount();

    private async void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        string id = TxtModId.Text.Trim();
        if (!long.TryParse(id, out _))
        {
            TxtStatus.Text = "请输入纯数字的工坊编号";
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(s =>
        {
            TxtStatus.Text = s;
            TxtStatus.ToolTip = s;
        });
        BtnDownload.IsEnabled = false;
        try
        {
            await _mods.DownloadAsync(id, progress, _cts.Token);
        }
        catch (OperationCanceledException) { TxtStatus.Text = "已取消"; }
        catch (Exception ex) { TxtStatus.Text = "失败: " + ex.Message; }
        finally
        {
            BtnDownload.IsEnabled = true;
            RefreshList();
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshList();

    private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(s =>
        {
            TxtStatus.Text = s;
            TxtStatus.ToolTip = s;
        });
        BtnCheckUpdates.IsEnabled = false;
        try
        {
            var installed = _mods.ListInstalled();
            foreach (var m in installed)
            {
                TxtStatus.Text = $"更新 {m.Name}（{m.Id}）...";
                await _mods.DownloadAsync(m.Id, progress, _cts.Token);
            }
            TxtStatus.Text = $"全部 {installed.Count} 个 mod 检查完成";
        }
        catch (OperationCanceledException) { TxtStatus.Text = "已取消"; }
        catch (Exception ex) { TxtStatus.Text = "失败: " + ex.Message; }
        finally
        {
            BtnCheckUpdates.IsEnabled = true;
            RefreshList();
        }
    }

    private void LstMods_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = LstMods.SelectedItem as InstalledMod;
        BuildOptionPanel(_selected);
    }

    private void BuildOptionPanel(InstalledMod? mod)
    {
        PnlOptions.Children.Clear();
        _optionControls.Clear();
        if (mod == null)
        {
            TxtModTitle.Text = "未选择 mod";
            TxtCfgHint.Text = "";
            return;
        }

        string modinfo = Path.Combine(mod.Folder, "modinfo.lua");
        if (!File.Exists(modinfo))
        {
            TxtModTitle.Text = $"{mod.Name}（{mod.Id}）";
            PnlOptions.Children.Add(new TextBlock
            {
                Text = "该 mod 尚未下载到服务器目录。点击上方「下载 Mod」，或在启动服务器时自动下载后刷新。",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Gray,
            });
            TxtCfgHint.Text = "";
            return;
        }

        var meta = ModInfoParser.ParseFile(modinfo);
        TxtModTitle.Text = $"{meta.Name}（{mod.Id}）";

        if (meta.Options.Count == 0)
        {
            PnlOptions.Children.Add(new TextBlock
            {
                Text = "这个 mod 没有可配置项。",
                Foreground = Brushes.Gray,
            });
            TxtCfgHint.Text = "";
            return;
        }

        var overrides = _mods.LoadOverrides();
        overrides.TryGetValue(mod.Id, out var entry);

        foreach (var opt in meta.Options)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            var label = new TextBlock
            {
                Text = opt.Label,
                Width = 190,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = string.IsNullOrWhiteSpace(opt.Hover) ? opt.Name : opt.Hover,
            };
            stack.Children.Add(label);

            object? current = entry != null && entry.Options.TryGetValue(opt.Name, out var v) ? v : opt.Default;

            FrameworkElement control;
            if (opt.Options.Count > 0)
            {
                var cmb = new ComboBox { Width = 320, VerticalAlignment = VerticalAlignment.Center };
                int selIdx = 0;
                for (int i = 0; i < opt.Options.Count; i++)
                {
                    var item = opt.Options[i];
                    cmb.Items.Add(item);
                    if (Equals(item.Data, current)) selIdx = i;
                }
                cmb.SelectedIndex = selIdx;
                control = cmb;
            }
            else if (current is bool)
            {
                var chk = new CheckBox
                {
                    IsChecked = (bool)current,
                    VerticalAlignment = VerticalAlignment.Center,
                    Content = "启用",
                };
                control = chk;
            }
            else if (current is double or int or long or float)
            {
                var tb = new TextBox
                {
                    Text = Convert.ToString(current, CultureInfo.InvariantCulture) ?? "",
                    Width = 120,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(4, 2, 4, 2),
                };
                control = tb;
            }
            else
            {
                var tb = new TextBox
                {
                    Text = current?.ToString() ?? "",
                    Width = 320,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(4, 2, 4, 2),
                };
                control = tb;
            }
            stack.Children.Add(control);
            PnlOptions.Children.Add(stack);
            _optionControls.Add((opt, control));
        }
        TxtCfgHint.Text = $"共 {meta.Options.Count} 个可配置项";
    }

    private object? ReadValue(FrameworkElement ctl)
    {
        switch (ctl)
        {
            case ComboBox cmb when cmb.SelectedItem is OptionItem oi:
                return oi.Data;
            case CheckBox chk:
                return chk.IsChecked == true;
            case TextBox tb:
                string t = tb.Text.Trim();
                if (long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return l;
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
                return t;
            default:
                return null;
        }
    }

    private void BtnSaveCfg_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;

        var overrides = _mods.LoadOverrides();
        if (!overrides.TryGetValue(_selected.Id, out var entry))
        {
            entry = new OverrideEntry { Enabled = _selected.Enabled };
            overrides[_selected.Id] = entry;
        }
        entry.Options.Clear();
        foreach (var (opt, ctl) in _optionControls)
        {
            var val = ReadValue(ctl);
            if (val != null) entry.Options[opt.Name] = val;
        }

        _mods.SaveOverrides(Path.Combine(_clusterDir, "Master"), overrides);
        _mods.SaveOverrides(Path.Combine(_clusterDir, "Caves"), overrides);
        TxtCfgHint.Text = "已保存到 Master 与 Caves 的 modoverrides.lua";
    }

    private void ModCheckedChanged(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox chk && chk.DataContext is InstalledMod mod)
        {
            _mods.SetEnabled(mod.Id, chk.IsChecked == true);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e) => DeleteCheckedOrSelected();

    private void BtnDeleteChecked_Click(object sender, RoutedEventArgs e) => DeleteCheckedOrSelected();

    /// <summary>删除勾选的 mod；若一个都没勾，则删除当前选中项。</summary>
    private void DeleteCheckedOrSelected()
    {
        var targets = CheckedMods;
        if (targets.Count == 0)
        {
            if (_selected != null) targets = new List<InstalledMod> { _selected };
            else
            {
                MessageBox.Show(this, "请先勾选要删除的 mod（或选中一行）。",
                    "删除", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        string names = string.Join("、", targets.Take(6).Select(m => $"{m.Name}({m.Id})"));
        if (targets.Count > 6) names += $" 等 {targets.Count} 个";
        var r = MessageBox.Show(this,
            $"确定删除 {targets.Count} 个 mod？\n{names}\n\n将移除文件并取消启用。",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        int done = 0;
        foreach (var m in targets)
        {
            try
            {
                _mods.Delete(m.Id);
                done++;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"删除 {m.Id} 失败: {ex.Message}";
            }
        }
        _selected = null;
        RefreshList();
        BuildOptionPanel(null);
        TxtStatus.Text = $"已删除 {done} 个 mod";
    }
}
