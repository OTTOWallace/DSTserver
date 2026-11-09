using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DSTserver.Models;
using DSTserver.Services;

namespace DSTserver;

public partial class RollbackWindow : Window
{
    private readonly string _clusterDir;
    private readonly Action<string> _log;
    private readonly Func<bool> _isRunning;
    private readonly Action _stopServer;
    private List<BackupInfo> _all = new();
    private bool _suppressSelectAll;

    public RollbackWindow(AppSettings settings, Action<string> log, Func<bool> isRunning, Action stopServer)
    {
        InitializeComponent();
        _clusterDir = AppPaths.ClusterDir(settings.Cluster.ClusterSlot);
        _log = log;
        _isRunning = isRunning;
        _stopServer = stopServer;
        Title = $"存档回档（存档 {settings.Cluster.ClusterSlot} · {SaveService.GetSlotName(settings.Cluster.ClusterSlot)}）";

        CmbRange.Items.Add(new ComboBoxItem { Content = "全部", Tag = 0 });
        CmbRange.Items.Add(new ComboBoxItem { Content = "最近 1 天", Tag = 1 });
        CmbRange.Items.Add(new ComboBoxItem { Content = "最近 3 天", Tag = 3 });
        CmbRange.Items.Add(new ComboBoxItem { Content = "最近 7 天", Tag = 7 });
        CmbRange.Items.Add(new ComboBoxItem { Content = "最近 30 天", Tag = 30 });
        CmbRange.SelectedIndex = 0;

        RefreshList();
    }

    private IEnumerable<BackupInfo> CurrentBackups =>
        LstBackups.ItemsSource as IEnumerable<BackupInfo> ?? Enumerable.Empty<BackupInfo>();

    private List<BackupInfo> CheckedBackups => CurrentBackups.Where(b => b.Selected).ToList();

    private void RefreshList()
    {
        _all = BackupService.ListBackups(_clusterDir);
        int days = CmbRange.SelectedItem is ComboBoxItem cbi && cbi.Tag is int d ? d : 0;
        var filtered = days <= 0
            ? _all
            : _all.Where(b => (DateTime.Now - b.Time).TotalDays <= days).ToList();
        LstBackups.ItemsSource = null;
        LstBackups.ItemsSource = filtered;
        _suppressSelectAll = true;
        ChkSelectAll.IsChecked = false;
        _suppressSelectAll = false;
        RefreshCheckedCount();
        TxtStatus.Text = $"共 {filtered.Count} 个备份";
    }

    private void RefreshCheckedCount() =>
        TxtCheckedCount.Text = $"已勾选 {CheckedBackups.Count} 个";

    private void ChkSelectAll_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressSelectAll) return;
        bool on = ChkSelectAll.IsChecked == true;
        foreach (var b in CurrentBackups) b.Selected = on;
        RefreshCheckedCount();
    }

    private void BackupSelChanged(object sender, RoutedEventArgs e) => RefreshCheckedCount();

    private void BtnDeleteChecked_Click(object sender, RoutedEventArgs e)
    {
        var targets = CheckedBackups;
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "请先勾选要删除的备份。", "删除备份",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var r = MessageBox.Show(this,
            $"确定删除 {targets.Count} 个备份？\n此操作不可撤销。",
            "确认删除备份", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;

        int done = 0;
        foreach (var b in targets)
        {
            try
            {
                BackupService.DeleteBackup(b.Path);
                _log($"备份已删除：{b.Display}");
                done++;
            }
            catch (Exception ex)
            {
                _log($"删除备份失败 {b.Display}: {ex.Message}");
            }
        }
        RefreshList();
        TxtStatus.Text = $"已删除 {done} 个备份";
    }

    private void CmbRange_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshList();

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshList();

    private void LstBackups_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstBackups.SelectedItem is BackupInfo b)
            TxtStatus.Text = $"已选：{b.Display}（{b.Path}）";
    }

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        if (LstBackups.SelectedItem is not BackupInfo b)
        {
            MessageBox.Show(this, "请先选择一个备份。", "回档", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var r = MessageBox.Show(this,
            $"将用 {b.Display} 的备份覆盖当前存档（当前 Master/Caves 会被删除）。\n确定回档？",
            "确认回档", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;

        try
        {
            if (_isRunning())
            {
                _stopServer();
                _log("已停止服务器（回档需要）");
            }
            BackupService.Restore(_clusterDir, b.Path);
            _log($"回档完成：{b.Display}");
            MessageBox.Show(this, "回档完成，可以重新启动服务器。", "回档",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "回档失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
