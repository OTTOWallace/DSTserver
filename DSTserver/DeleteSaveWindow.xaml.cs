using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DSTserver.Services;

namespace DSTserver;

public partial class DeleteSaveWindow : Window
{
    private readonly Action<string> _log;
    private readonly Func<bool> _isRunning;
    private bool _suppressSelectAll;

    public DeleteSaveWindow(Action<string> log, Func<bool> isRunning)
    {
        InitializeComponent();
        _log = log;
        _isRunning = isRunning;
        RefreshList();
    }

    private void RefreshList()
    {
        var list = SaveService.ListSlots();
        LstSlots.ItemsSource = null;
        LstSlots.ItemsSource = list;
        _suppressSelectAll = true;
        ChkSelectAll.IsChecked = false;
        _suppressSelectAll = false;
        RefreshCheckedCount();
        TxtStatus.Text = $"共 {list.Count(s => s.HasSave)} 个槽位有存档";
    }

    private List<SlotInfo> CheckedSlots =>
        (LstSlots.ItemsSource as IEnumerable<SlotInfo> ?? Enumerable.Empty<SlotInfo>())
        .Where(s => s.Selected).ToList();

    private void RefreshCheckedCount() =>
        TxtCheckedCount.Text = $"已勾选 {CheckedSlots.Count} 个";

    private void ChkSelectAll_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressSelectAll) return;
        bool on = ChkSelectAll.IsChecked == true;
        foreach (var s in LstSlots.ItemsSource as IEnumerable<SlotInfo> ?? Enumerable.Empty<SlotInfo>())
        {
            if (s.HasSave) s.Selected = on;
        }
        RefreshCheckedCount();
    }

    private void SlotSelChanged(object sender, RoutedEventArgs e) => RefreshCheckedCount();

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var targets = CheckedSlots;
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "请先勾选要删除存档的槽位。", "删除存档",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_isRunning())
        {
            MessageBox.Show(this, "服务器正在运行，请先停止服务器再删除存档。", "删除存档",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string names = string.Join("、", targets.Select(s => $"存档 {s.Slot}（{s.Name}）"));
        bool backupFirst = ChkBackupFirst.IsChecked == true;
        var r = MessageBox.Show(this,
            $"确定删除以下 {targets.Count} 个槽位的世界存档？\n{names}\n\n" +
            $"{(backupFirst ? "删除前会先自动备份一次。\n" : "不备份，直接删除。\n")}" +
            "删除后对应世界将重新生成，此操作不可撤销。",
            "确认删除存档", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;

        int done = 0;
        foreach (var s in targets)
        {
            string dir = AppPaths.ClusterDir(s.Slot);
            try
            {
                if (backupFirst)
                {
                    string b = BackupService.CreateBackup(dir);
                    if (!string.IsNullOrEmpty(b)) _log($"存档 {s.Slot} 已备份: {b}");
                }
                SaveService.DeleteSaves(dir);
                _log($"存档 {s.Slot}（{s.Name}）的世界存档已删除");
                done++;
            }
            catch (Exception ex)
            {
                _log($"删除存档 {s.Slot} 失败: {ex.Message}");
            }
        }

        RefreshList();
        TxtStatus.Text = $"已删除 {done} 个槽位的存档";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
