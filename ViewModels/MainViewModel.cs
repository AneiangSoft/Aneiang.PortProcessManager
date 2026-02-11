using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortProcessManager.Services;

namespace PortProcessManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _autoRefreshTimer;
    private readonly HashSet<string> _pendingVerifications = new();

    public ObservableCollection<PortRow> Items { get; } = new();

    public ICollectionView ItemsView { get; }

    [ObservableProperty]
    private string query = string.Empty;

    [ObservableProperty]
    private PortRow? selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyHint))]
    private bool isBusy;

    public bool ShowEmptyHint => !IsBusy && Items.Count == 0;

    [ObservableProperty]
    private bool isAutoRefreshEnabled;

    [ObservableProperty]
    private string statusText = "就绪";

    [ObservableProperty]
    private int totalConnections;

    [ObservableProperty]
    private int tcpCount;

    [ObservableProperty]
    private int udpCount;

    [ObservableProperty]
    private int processCount;

    [ObservableProperty]
    private bool isGrouped;

    [ObservableProperty]
    private bool areGroupsExpanded = false;

    [ObservableProperty]
    private List<PortRow> selectedItemsSnapshot = new();

    public MainViewModel()
    {
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = Filter;

        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autoRefreshTimer.Tick += async (s, e) => 
        {
            // s3: 自动刷新节流，忙碌时跳过
            if (!IsBusy) await RefreshAsync();
        };

        _ = RefreshAsync();
    }

    partial void OnIsGroupedChanged(bool value)
    {
        bool wasAutoRefreshEnabled = IsAutoRefreshEnabled;
        if (wasAutoRefreshEnabled)
        {
            _autoRefreshTimer.Stop();
        }

        using (ItemsView.DeferRefresh())
        {
            ItemsView.GroupDescriptions.Clear();
            if (value)
            {
                ItemsView.GroupDescriptions.Add(new PropertyGroupDescription("GroupKey"));
            }
        }

        if (wasAutoRefreshEnabled)
        {
            _autoRefreshTimer.Start();
        }
    }

    [RelayCommand]
    private void ToggleGroupsExpansion()
    {
        AreGroupsExpanded = !AreGroupsExpanded;
    }

    [RelayCommand]
    private void KillGroup(object? parameter)
    {
        // 临时增加调试弹窗，确认命令是否触发
        // MessageBox.Show($"调试：KillGroup 已触发，参数类型: {parameter?.GetType().Name}"); 
        
        FileLogger.Info($"收到结束进程组请求，原始参数类型: {parameter?.GetType().Name}, 值: {parameter}");

        string? groupKey = null;
        if (parameter is PortRow row)
        {
            groupKey = row.GroupKey;
        }
        else if (parameter is string s)
        {
            groupKey = s;
        }
        else if (parameter != null && parameter.GetType().Name == "CollectionViewGroupInternal")
        {
            // 处理 WPF 内部的分组对象
            dynamic group = parameter;
            groupKey = group.Name?.ToString();
        }
        else if (parameter is System.Windows.Data.CollectionViewGroup cvg)
        {
            groupKey = cvg.Name?.ToString();
        }

        if (string.IsNullOrEmpty(groupKey))
        {
            FileLogger.Warn("无法识别的分组 Key，操作取消");
            return;
        }

        FileLogger.Info($"解析后的 GroupKey: {groupKey}");

        // s1: 查找该组下所有的安全 PID
        var allInGroup = Items.Where(x => x.GroupKey == groupKey).ToList();
        var protectedItems = allInGroup.Where(PortInfoProvider.IsProtected).ToList();
        var pidsToKill = allInGroup
                        .Where(x => !PortInfoProvider.IsProtected(x) && x.Pid > 4)
                        .Select(x => x.Pid)
                        .Distinct()
                        .ToList();

        FileLogger.Info($"该组共有 {allInGroup.Count} 条记录，受保护 {protectedItems.Count} 条，待结束唯一 PID 数: {pidsToKill.Count}");

        if (pidsToKill.Count == 0)
        {
            MessageBox.Show($"操作拒绝：分组 \"{groupKey}\" 下的内容全部受系统保护，不允许结束。", 
                "系统保护", MessageBoxButton.OK, MessageBoxImage.Stop);
            return;
        }

        var confirmMsg = $"您确定要结束 \"{groupKey}\" 关联的所有安全进程吗？";
        if (protectedItems.Count > 0)
        {
            confirmMsg += $"\n\n注意：将自动跳过该组内 {protectedItems.Count} 个受保护的系统连接。";
        }

        var result = MessageBox.Show(confirmMsg, "确认结束进程组", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        // s3: 记录受影响的端口 UniqueKeys 用于后续校验
        foreach (var r in allInGroup.Where(x => !PortInfoProvider.IsProtected(x)))
        {
            _pendingVerifications.Add(r.UniqueKey);
        }

        ExecuteKillBatch(pidsToKill, protectedItems.Count);
    }

    private void ExecuteKillBatch(List<int> pids, int skipCount)
    {
        int successCount = 0;
        int failCount = 0;

        foreach (var pid in pids)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill(entireProcessTree: true);
                if (proc.WaitForExit(2000)) successCount++;
                else failCount++;
            }
            catch { failCount++; }
        }

        StatusText = $"操作完成：成功 {successCount}，失败 {failCount}";
        if (skipCount > 0) StatusText += $" (跳过 {skipCount} 个系统项)";
        
        FileLogger.Info($"批量结束进程: 成功 {successCount}, 失败 {failCount}, 跳过保护 {skipCount}");
        _ = RefreshAsync();
    }

    private void UpdateStatistics()
    {
        TotalConnections = Items.Count;
        TcpCount = Items.Count(x => x.Protocol == ProtocolType.TCP);
        UdpCount = Items.Count(x => x.Protocol == ProtocolType.UDP);
        ProcessCount = Items.Where(x => x.Pid > 0).Select(x => x.Pid).Distinct().Count();
    }

    partial void OnIsAutoRefreshEnabledChanged(bool value)
    {
        if (value)
            _autoRefreshTimer.Start();
        else
            _autoRefreshTimer.Stop();
        
        FileLogger.Info($"自动刷新已{(value ? "开启" : "关闭")}");
    }

    partial void OnQueryChanged(string value)
    {
        ItemsView.Refresh();
        StatusText = $"{ItemsView.Cast<object>().Count()} 条";
    }

    private bool Filter(object obj)
    {
        if (obj is not PortRow row) return false;
        if (string.IsNullOrWhiteSpace(Query)) return true;

        var q = Query.Trim();
        return row.LocalPort.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)
               || row.Pid.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)
               || row.ProcessName.Contains(q, StringComparison.OrdinalIgnoreCase)
               || row.ProcessPath.Contains(q, StringComparison.OrdinalIgnoreCase)
               || row.LocalAddress.Contains(q, StringComparison.OrdinalIgnoreCase)
               || row.Protocol.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)
               || row.State.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            StatusText = "刷新中...";

            var oldDataDict = Items
                .GroupBy(x => x.UniqueKey)
                .ToDictionary(g => g.Key, g => g.First().State);
            var lastSelectedKey = SelectedItem?.UniqueKey;

            var sw = Stopwatch.StartNew();
            var newData = await Task.Run(PortInfoProvider.GetPortProcesses);
            sw.Stop();

            var dedupedData = newData
                .GroupBy(x => x.UniqueKey)
                .Select(g => g.First())
                .OrderByDescending(x => x.LocalPort)
                .ToList();

            Items.Clear();
            foreach (var row in dedupedData)
            {
                if (oldDataDict.TryGetValue(row.UniqueKey, out var oldState))
                {
                    if (oldState != row.State)
                        row.ChangeState = RowChangeState.Changed;
                }
                else if (oldDataDict.Count > 0)
                {
                    row.ChangeState = RowChangeState.New;
                }
                Items.Add(row);
            }

            UpdateStatistics();

            // s3: 闭环检测 - 检查之前待校验的端口是否已释放
            if (_pendingVerifications.Count > 0)
            {
                var currentKeys = new HashSet<string>(dedupedData.Select(x => x.UniqueKey));
                int released = 0;
                int lingering = 0;
                int timeWait = 0;

                foreach (var key in _pendingVerifications.ToList())
                {
                    if (!currentKeys.Contains(key))
                    {
                        released++;
                        _pendingVerifications.Remove(key);
                    }
                    else
                    {
                        var row = dedupedData.First(x => x.UniqueKey == key);
                        if (row.State == "TIME_WAIT") timeWait++;
                        else lingering++;
                    }
                }

                if (released > 0 || lingering > 0 || timeWait > 0)
                {
                    string verifyMsg = $"检测到释放结果：已关闭 {released} 个端口";
                    if (timeWait > 0) verifyMsg += $"，{timeWait} 个处于回收中(TIME_WAIT)";
                    if (lingering > 0) verifyMsg += $"，{lingering} 个仍被占用";
                    StatusText = verifyMsg;
                    FileLogger.Info(verifyMsg);
                }
            }

            if (lastSelectedKey != null)
            {
                SelectedItem = Items.FirstOrDefault(x => x.UniqueKey == lastSelectedKey);
            }

            ItemsView.Refresh();
            StatusText = string.IsNullOrEmpty(StatusText) || StatusText == "刷新中..." 
                ? $"{Items.Count} 条，耗时 {sw.ElapsedMilliseconds} ms" 
                : StatusText;

            FileLogger.Info($"端口扫描完成: {Items.Count} 条记录, 耗时 {sw.ElapsedMilliseconds}ms");

            if (Items.Any(x => x.ChangeState != RowChangeState.None))
            {
                _ = Task.Delay(2000).ContinueWith(_ => 
                {
                    Application.Current.Dispatcher.Invoke(() => 
                    {
                        foreach (var item in Items)
                        {
                            item.ChangeState = RowChangeState.None;
                        }
                    });
                });
            }
        }
        catch (Exception ex)
        {
            StatusText = $"刷新失败：{ex.Message}";
            FileLogger.Error("刷新失败", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanKillSelected))]
    private void KillSelected()
    {
        if (SelectedItem is null) return;

        if (PortInfoProvider.IsProtected(SelectedItem))
        {
            MessageBox.Show($"操作拒绝：进程 \"{SelectedItem.ProcessName}\" 是系统关键进程，不允许手动结束。", 
                "系统保护", MessageBoxButton.OK, MessageBoxImage.Stop);
            return;
        }

        var result = MessageBox.Show(
            $"您确定要结束进程 \"{SelectedItem.ProcessName}\" (PID: {SelectedItem.Pid}) 吗？",
            "确认结束进程", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        // s3: 记录受影响端口
        _pendingVerifications.Add(SelectedItem.UniqueKey);

        ExecuteKillBatch(new List<int> { SelectedItem.Pid }, 0);
    }

    [RelayCommand]
    private void KillSelectedItems()
    {
        if (SelectedItemsSnapshot == null || SelectedItemsSnapshot.Count == 0) return;

        var protectedItems = SelectedItemsSnapshot.Where(PortInfoProvider.IsProtected).ToList();
        var pidsToKill = SelectedItemsSnapshot
            .Where(x => !PortInfoProvider.IsProtected(x) && x.Pid > 4)
            .Select(x => x.Pid)
            .Distinct()
            .ToList();

        if (pidsToKill.Count == 0)
        {
            string msg = protectedItems.Count > 0 
                ? $"操作拒绝：选中的进程全部受系统保护，无法结束。" 
                : "没有可结束的进程。";
            MessageBox.Show(msg, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmMsg = $"您确定要结束选中的 {pidsToKill.Count} 个唯一进程吗？";
        if (protectedItems.Count > 0) confirmMsg += $"\n\n注意：将自动跳过 {protectedItems.Count} 个受保护的系统项。";

        var result = MessageBox.Show(confirmMsg, "确认批量结束", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        // s3: 记录受影响端口
        foreach (var item in SelectedItemsSnapshot.Where(x => !PortInfoProvider.IsProtected(x)))
        {
            _pendingVerifications.Add(item.UniqueKey);
        }

        ExecuteKillBatch(pidsToKill, protectedItems.Count);
    }

    [RelayCommand]
    private void CopySelectedItems()
    {
        if (SelectedItemsSnapshot == null || SelectedItemsSnapshot.Count == 0) return;

        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("协议\t本地地址\t本地端口\t远程地址\t远程端口\t状态\tPID\t进程\t用户\t路径");
            foreach (var row in SelectedItemsSnapshot)
            {
                sb.AppendLine($"{row.Protocol}\t{row.LocalAddress}\t{row.LocalPort}\t{row.RemoteAddress}\t{row.RemotePort}\t{row.State}\t{row.Pid}\t{row.ProcessName}\t{row.Owner}\t{row.ProcessPath}");
            }

            Clipboard.SetText(sb.ToString());
            StatusText = $"已批量复制 {SelectedItemsSnapshot.Count} 行信息";
        }
        catch (Exception ex)
        {
            StatusText = $"批量复制失败: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenFileLocation))]
    private void OpenFileLocation()
    {
        if (SelectedItem == null || !CanOpenFileLocation())
            return;

        try
        {
            Process.Start("explorer.exe", $"/select,\"{SelectedItem.ProcessPath}\"");
            FileLogger.Info($"打开文件位置: {SelectedItem.ProcessPath}");
        }
        catch (Exception ex)
        {
            StatusText = $"无法打开位置: {ex.Message}";
            FileLogger.Error($"无法打开位置: {SelectedItem.ProcessPath}", ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteOnSelected))]
    private void CopyInfo(string type)
    {
        if (SelectedItem == null) return;
        
        string text = type switch
        {
            "PID" => SelectedItem.Pid.ToString(),
            "Port" => SelectedItem.LocalPort.ToString(),
            "Path" => SelectedItem.ProcessPath,
            _ => $"{SelectedItem.Protocol} {SelectedItem.LocalAddress}:{SelectedItem.LocalPort} {SelectedItem.ProcessName} ({SelectedItem.Pid})"
        };

        try
        {
            Clipboard.SetText(text);
            StatusText = $"已复制 {type} 到剪贴板";
        }
        catch (Exception ex)
        {
            StatusText = $"复制失败: {ex.Message}";
        }
    }

    private bool CanKillSelected() => SelectedItem != null && !PortInfoProvider.IsProtected(SelectedItem);

    private bool CanOpenFileLocation() => 
        SelectedItem != null && 
        !string.IsNullOrEmpty(SelectedItem.ProcessPath) && 
        !SelectedItem.ProcessPath.StartsWith('[');

    private bool CanExecuteOnSelected() => SelectedItem != null;

    partial void OnSelectedItemChanged(PortRow? value)
    {
        KillSelectedCommand.NotifyCanExecuteChanged();
        OpenFileLocationCommand.NotifyCanExecuteChanged();
        CopyInfoCommand.NotifyCanExecuteChanged();
    }
}
