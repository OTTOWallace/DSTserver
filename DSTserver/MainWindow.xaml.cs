using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DSTserver.Models;
using DSTserver.Services;

namespace DSTserver;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private AppSettings _settings;
    private readonly ServerController _controller = new();
    private readonly ObservableCollection<PlayerInfo> _players = new();
    private CancellationTokenSource? _installCts;
    private int _logLines;
    private bool _suppressSlotEvent;
    private bool _busyBackup;
    private bool _busyModCheck;
    private readonly DispatcherTimer _playerTimer = new();
    private readonly DispatcherTimer _schedTimer = new();
    private readonly DispatcherTimer _watchdogTimer = new();

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        LstPlayers.ItemsSource = _players;
        CmbItem.SelectedIndex = 0;
        LoadUi();
        RefreshSlotList();
        _controller.Log += OnServerLog;
        _controller.PlayerListUpdated += OnPlayerList;
        _controller.LobbyTokenConflict += OnLobbyTokenConflict;
        RefreshServerPathText();
        RefreshScheduleText();

        _playerTimer.Interval = TimeSpan.FromSeconds(5);
        _playerTimer.Tick += (s, e) =>
        {
            if (_controller.IsRunning) _controller.SendCommand(ServerController.PlayerListCommand);
        };
        _playerTimer.Start();

        _schedTimer.Interval = TimeSpan.FromSeconds(30);
        _schedTimer.Tick += (s, e) => RunScheduledTasks();
        _schedTimer.Start();

        // 窗口自愈看门狗：若窗口被外部手段隐藏（WS_VISIBLE 被清），自动恢复显示。
        _watchdogTimer.Interval = TimeSpan.FromSeconds(5);
        _watchdogTimer.Tick += (s, e) => EnsureWindowVisible();
        _watchdogTimer.Start();
    }

    /// <summary>检测窗口是否被隐藏，若是则恢复显示（不影响最小化）。</summary>
    private void EnsureWindowVisible()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero && !IsWindowVisible(hwnd))
            {
                ShowWindow(hwnd, 9); // SW_RESTORE
                ShowWindow(hwnd, 5); // SW_SHOW
                AppendLog("窗口自愈：检测到窗口被隐藏，已自动恢复显示");
            }
        }
        catch { }
    }

    // ---------- UI 加载 / 保存 ----------

    private void LoadUi()
    {
        var c = _settings.Cluster;
        TxtClusterName.Text = c.ClusterName;
        TxtPassword.Text = c.Password;
        TxtMaxPlayers.Text = c.MaxPlayers.ToString();
        TxtToken.Text = c.Token;
        ChkPvp.IsChecked = c.Pvp;
        ChkPause.IsChecked = c.PauseWhenEmpty;
        ChkCaves.IsChecked = c.EnableCaves;
        foreach (var item in CmbGameMode.Items)
        {
            if (item is ComboBoxItem cbi && (string)cbi.Tag == c.GameMode)
            {
                CmbGameMode.SelectedItem = item;
                break;
            }
        }
        if (CmbGameMode.SelectedItem == null && CmbGameMode.Items.Count > 0)
            CmbGameMode.SelectedIndex = 0;
    }

    private void CollectUi()
    {
        var c = _settings.Cluster;
        c.ClusterName = TxtClusterName.Text.Trim();
        c.Password = TxtPassword.Text;
        c.MaxPlayers = int.TryParse(TxtMaxPlayers.Text.Trim(), out int mp) ? Math.Clamp(mp, 1, 64) : 6;
        c.Pvp = ChkPvp.IsChecked == true;
        c.PauseWhenEmpty = ChkPause.IsChecked == true;
        c.EnableCaves = ChkCaves.IsChecked == true;
        c.Token = TxtToken.Text.Trim();
        if (CmbGameMode.SelectedItem is ComboBoxItem cbi) c.GameMode = (string)cbi.Tag;
    }

    private void SaveSettings()
    {
        CollectUi();
        _settings.Save();
    }

    private void RefreshServerPathText()
    {
        string exe = AppPaths.ServerExe(AppPaths.SteamCmdDir(_settings.SteamCmdDir));
        TxtServerPath.Text = File.Exists(exe)
            ? "服务器程序：已安装 ✓"
            : "服务器程序：未安装（需先点下方按钮安装）";
    }

    private void RefreshScheduleText()
    {
        string b = _settings.LastBackupUtc == DateTime.MinValue
            ? "从未备份"
            : "上次备份 " + _settings.LastBackupUtc.ToLocalTime().ToString("MM-dd HH:mm");
        string m = _settings.LastModCheckUtc == DateTime.MinValue
            ? "从未检查"
            : "上次 mod 检查 " + _settings.LastModCheckUtc.ToLocalTime().ToString("MM-dd HH:mm");
        TxtSchedule.Text =
            $"定时备份：{(_settings.EnableScheduledBackup ? $"每 {_settings.BackupIntervalHours} 小时" : "关")}（{b}）\n" +
            $"mod 更新检查：{(_settings.EnableModAutoCheck ? $"每 {_settings.ModCheckIntervalHours} 小时" : "关")}（{m}）";
    }

    private string CurrentClusterDir() => AppPaths.ClusterDir(_settings.Cluster.ClusterSlot);

    // ---------- 存档槽位 ----------

    private void RefreshSlotList()
    {
        _suppressSlotEvent = true;
        CmbSlot.Items.Clear();
        for (int slot = 1; slot <= 5; slot++)
        {
            string dir = AppPaths.ClusterDir(slot);
            string label = $"存档 {slot}";
            if (Directory.Exists(dir))
            {
                string name = ReadClusterName(dir);
                if (!string.IsNullOrEmpty(name)) label += $" — {name}";
                else label += " —（已创建）";
            }
            else
            {
                label += " —（未创建）";
            }
            var item = new ComboBoxItem { Content = label, Tag = slot };
            CmbSlot.Items.Add(item);
            if (slot == _settings.Cluster.ClusterSlot) CmbSlot.SelectedItem = item;
        }
        _suppressSlotEvent = false;
        if (CmbSlot.SelectedItem == null && CmbSlot.Items.Count > 0) CmbSlot.SelectedIndex = 0;
    }

    private static string ReadClusterName(string clusterDir)
    {
        try
        {
            string ini = Path.Combine(clusterDir, "cluster.ini");
            if (!File.Exists(ini)) return "";
            foreach (var line in File.ReadAllLines(ini))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"cluster_name\s*=\s*(.+)");
                if (m.Success) return m.Groups[1].Value.Trim();
            }
        }
        catch { }
        return "";
    }

    private void CmbSlot_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSlotEvent) return;
        if (CmbSlot.SelectedItem is ComboBoxItem cbi && cbi.Tag is int slot)
        {
            if (_controller.IsRunning)
            {
                MessageBox.Show(this, "请先停止服务器再切换存档。", "DSTserver", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshSlotList();
                return;
            }
            _settings.Cluster.ClusterSlot = slot;
            SaveSettings();
            AppendLog($"已切换到存档 {slot}（{AppPaths.ClusterDir(slot)}）");
            RefreshSlotList();
        }
    }

    // ---------- 安装 / 启停 ----------

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        _installCts?.Cancel();
        _installCts = new CancellationTokenSource();
        var progress = new Progress<string>(s =>
        {
            TxtInstallStatus.Text = s;
            AppendLog(s);
        });
        BtnInstall.IsEnabled = false;
        try
        {
            await SteamCmdService.InstallOrUpdateServerAsync(
                AppPaths.SteamCmdDir(_settings.SteamCmdDir), progress, _installCts.Token);
        }
        catch (OperationCanceledException) { AppendLog("安装已取消"); }
        catch (Exception ex) { AppendLog("安装失败: " + ex.Message); }
        finally
        {
            BtnInstall.IsEnabled = true;
            RefreshServerPathText();
        }
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        string exe = AppPaths.ServerExe(AppPaths.SteamCmdDir(_settings.SteamCmdDir));
        if (!File.Exists(exe))
        {
            MessageBox.Show(this, "服务器程序未安装，请先点击「安装 / 更新饥荒服务器」。",
                "DSTserver", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string clusterDir = CurrentClusterDir();
        ConfigWriter.WriteClusterIni(clusterDir, _settings.Cluster);
        ConfigWriter.WriteServerIni(Path.Combine(clusterDir, "Master"), true);
        if (_settings.Cluster.EnableCaves)
            ConfigWriter.WriteServerIni(Path.Combine(clusterDir, "Caves"), false);
        ConfigWriter.WriteToken(clusterDir, _settings.Cluster.Token);

        AppendLog("集群配置已写入 " + clusterDir);
        if (string.IsNullOrWhiteSpace(_settings.Cluster.Token))
            AppendLog("警告：未设置服务器令牌，服务器无法被公网/大厅发现。");

        _players.Clear();
        _controller.Start(exe, AppPaths.ClusterName(_settings.Cluster.ClusterSlot), _settings.Cluster.EnableCaves);
        TxtServerState.Text = "状态：运行中";
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _controller.Stop();
        TxtServerState.Text = "状态：已停止";
        AppendLog("服务器已停止");
    }

    // ---------- 玩家列表 ----------

    private void OnPlayerList(List<PlayerInfo> list)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _players.Clear();
            foreach (var p in list) _players.Add(p);
        }));
    }

    private void LstPlayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstPlayers.SelectedItem is PlayerInfo p)
            TxtSelectedPlayer.Text = $"目标：{p.Name}（{p.UserId}）";
        else
            TxtSelectedPlayer.Text = "未选中玩家";
    }

    private void BtnGive_Click(object sender, RoutedEventArgs e)
    {
        if (LstPlayers.SelectedItem is not PlayerInfo player)
        {
            MessageBox.Show(this, "请先在列表里选中一名在线玩家。", "DSTserver", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (CmbItem.SelectedItem is not GiveItem item)
        {
            MessageBox.Show(this, "请选择物品。", "DSTserver", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(TxtCount.Text.Trim(), out int count) || count <= 0 || count > 1000)
        {
            MessageBox.Show(this, "数量请输入 1-1000 的整数。", "DSTserver", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string cmd = ServerController.BuildGiveCommand(player.UserId, item.Prefab, count);
        if (_controller.SendCommand(cmd))
        {
            AppendLog($"已向 {player.Name} 发放 {count} × {item.Label}（{item.Prefab}）");
        }
    }

    // ---------- 控制台 ----------

    private void BtnSend_Click(object sender, RoutedEventArgs e)
    {
        string cmd = TxtConsole.Text.Trim();
        if (cmd.Length == 0) return;
        if (_controller.SendCommand(cmd))
        {
            AppendLog(">> " + cmd);
            TxtConsole.Clear();
        }
    }

    private void TxtConsole_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnSend_Click(sender, e);
            e.Handled = true;
        }
    }

    private void BtnSaveNow_Click(object sender, RoutedEventArgs e)
    {
        if (_controller.SendCommand("c_save()"))
            AppendLog(">> 已发送存档指令 c_save()");
    }

    private void BtnRollbackN_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtRollbackN.Text.Trim(), out int n) || n <= 0 || n > 100)
        {
            MessageBox.Show(this, "快照数请输入 1-100 的整数。", "DSTserver", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_controller.SendCommand($"c_rollback({n})"))
            AppendLog($">> 已发送回档指令 c_rollback({n})（回退 {n} 个快照）");
    }

    // ---------- 备份 / 回档 ----------

    private async void BtnBackupNow_Click(object sender, RoutedEventArgs e)
    {
        if (_busyBackup) return;
        _busyBackup = true;
        BtnBackupNow.IsEnabled = false;
        string clusterDir = CurrentClusterDir();
        AppendLog("开始备份 " + clusterDir + " ...");
        try
        {
            string result = await Task.Run(() => BackupService.CreateBackup(clusterDir));
            if (string.IsNullOrEmpty(result))
            {
                AppendLog("备份失败：集群目录不存在（先启动一次服务器生成世界）");
            }
            else
            {
                _settings.LastBackupUtc = DateTime.UtcNow;
                SaveSettings();
                AppendLog("备份完成: " + result);
            }
        }
        catch (Exception ex) { AppendLog("备份失败: " + ex.Message); }
        finally
        {
            _busyBackup = false;
            BtnBackupNow.IsEnabled = true;
            RefreshScheduleText();
        }
    }

    private void BtnRollback_Click(object sender, RoutedEventArgs e)
    {
        var win = new RollbackWindow(_settings, AppendLog,
            () => _controller.IsRunning,
            () => { _controller.Stop(); TxtServerState.Text = "状态：已停止"; })
        { Owner = this };
        win.ShowDialog();
    }

    // ---------- mod 更新检查 ----------

    private async void BtnModCheck_Click(object sender, RoutedEventArgs e)
    {
        await CheckModUpdatesAsync();
    }

    private async Task CheckModUpdatesAsync()
    {
        if (_busyModCheck) return;
        _busyModCheck = true;
        BtnModCheck.IsEnabled = false;
        string steamDir = AppPaths.SteamCmdDir(_settings.SteamCmdDir);
        string clusterDir = CurrentClusterDir();
        var mods = new ModService(steamDir, clusterDir);
        try
        {
            var installed = mods.ListInstalled();
            AppendLog($"开始检查 {installed.Count} 个 mod 的更新...");
            var progress = new Progress<string>(AppendLog);
            int done = 0;
            foreach (var m in installed)
            {
                AppendLog($"检查 {m.Name}（{m.Id}）...");
                await mods.DownloadAsync(m.Id, progress, CancellationToken.None);
                done++;
            }
            _settings.LastModCheckUtc = DateTime.UtcNow;
            SaveSettings();
            AppendLog($"mod 更新检查完成（{done} 个）");
        }
        catch (Exception ex) { AppendLog("mod 检查失败: " + ex.Message); }
        finally
        {
            _busyModCheck = false;
            BtnModCheck.IsEnabled = true;
            RefreshScheduleText();
        }
    }

    // ---------- 定时任务 ----------

    private async void RunScheduledTasks()
    {
        // 定时备份（服务器运行中则推迟）
        if (_settings.EnableScheduledBackup && !_busyBackup &&
            (DateTime.UtcNow - _settings.LastBackupUtc).TotalHours >= _settings.BackupIntervalHours)
        {
            if (_controller.IsRunning)
            {
                AppendLog("定时备份推迟：服务器运行中，待停止后执行");
            }
            else
            {
                BtnBackupNow_Click(this, new RoutedEventArgs());
            }
        }

        // 定时 mod 检查
        if (_settings.EnableModAutoCheck && !_busyModCheck &&
            (DateTime.UtcNow - _settings.LastModCheckUtc).TotalHours >= _settings.ModCheckIntervalHours)
        {
            await CheckModUpdatesAsync();
        }
    }

    // ---------- 其他 ----------

    private void BtnMods_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        if (!File.Exists(AppPaths.ServerExe(AppPaths.SteamCmdDir(_settings.SteamCmdDir))))
        {
            MessageBox.Show(this, "服务器程序未安装，请先安装服务器。",
                "DSTserver", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var win = new ModManagerWindow(_settings) { Owner = this };
        win.ShowDialog();
    }

    private void BtnWorld_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        var win = new WorldSettingsWindow(_settings) { Owner = this };
        win.ShowDialog();
    }

    private void BtnToken_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://accounts.klei.com/account/game/servers?game=DST")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "无法打开浏览器", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnTokenPaste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText()) TxtToken.Text = Clipboard.GetText().Trim();
            else
            {
                MessageBox.Show(this, "剪贴板里没有文本。", "DSTserver", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch { }
        SaveSettings();
        ConfigWriter.WriteToken(CurrentClusterDir(), _settings.Cluster.Token);
        AppendLog(string.IsNullOrWhiteSpace(_settings.Cluster.Token)
            ? "未写入令牌"
            : "令牌已保存到 " + Path.Combine(CurrentClusterDir(), "cluster_token.txt"));
    }

    private void BtnLogClear_Click(object sender, RoutedEventArgs e)
    {
        TxtLog.Clear();
        _logLines = 0;
    }

    private void OnServerLog(string line) =>
        Dispatcher.BeginInvoke(new Action(() => AppendLog(line)));

    private void OnLobbyTokenConflict() =>
        Dispatcher.BeginInvoke(new Action(() =>
        {
            MessageBox.Show(this,
                "服务器令牌已被占用！\n\n" +
                "大厅注册失败（E_ROWID_EXIST）：当前填写的服务器令牌\n" +
                "正被另一个在线服务器使用。\n\n" +
                "解决办法：\n" +
                "1. 去 Klei 官网为这个服务器新生成一个令牌（点左侧「获取令牌」）\n" +
                "2. 或者停掉正在使用同一令牌的另外一台服务器\n\n" +
                "提示：更换令牌后需要重启服务器才能生效。",
                "大厅注册失败",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }));

    private void AppendLog(string line)
    {
        TxtLog.AppendText(line + Environment.NewLine);
        if (++_logLines > 3000)
        {
            TxtLog.Clear();
            _logLines = 0;
        }
        TxtLog.ScrollToEnd();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _playerTimer.Stop();
        _schedTimer.Stop();
        _watchdogTimer.Stop();
        SaveSettings();
        _controller.Stop();
    }
}
