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

    public RollbackWindow(AppSettings settings, Action<string> log, Func<bool> isRunning, Action stopServer)
    {
        InitializeComponent();
        _clusterDir = AppPaths.ClusterDir(settings.Cluster.ClusterSlot);
        _log = log;
        _isRunning = isRunning;
        _stopServer = stopServer;

        CmbRange.Items.Add(new ComboBoxItem { Content = "全部", Tag = 0 });
        CmbRange.Items.Add(new ComboBoxItem { Content = "最近 1 天", Tag = 1 });
        CmbRange.Items.Add(new ComboBoxItem { Content = "最近 3 天", Tag = 3 });
        CmbRange.Items.Add(new ComboBoxItem { Content = "最近 7 天", Tag = 7 });
        CmbRange.Items.Add(new ComboBoxItem { Content = "最近 30 天", Tag = 30 });
        CmbRange.SelectedIndex = 0;

        RefreshList();
    }

    private void RefreshList()
    {
        _all = BackupService.ListBackups(_clusterDir);
        int days = CmbRange.SelectedItem is ComboBoxItem cbi && cbi.Tag is int d ? d : 0;
        var filtered = days <= 0
            ? _all
            : _all.Where(b => (DateTime.Now - b.Time).TotalDays <= days).ToList();
        LstBackups.ItemsSource = null;
        LstBackups.ItemsSource = filtered;
        TxtStatus.Text = $"共 {filtered.Count} 个备份";
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
