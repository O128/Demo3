using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using 点胶机.Core.Interfaces;
using 点胶机.Data;
using 点胶机.Data.Entities;

namespace 点胶机.ViewModels;

/// <summary>报警页 —— 当前报警 + 历史报警查询(从 MySQL)+ Ack 确认</summary>
public partial class AlarmViewModel : ViewModelBase
{
    private readonly IAlarmService _alarmSvc;
    private readonly AlarmRepository _repo;

    /// <summary>当前活跃报警(实时)</summary>
    public ObservableCollection<AlarmRecord> ActiveAlarms { get; } = new();

    /// <summary>历史报警(从 MySQL 查询)</summary>
    public ObservableCollection<AlarmEntity> HistoryAlarms { get; } = new();

    [ObservableProperty] private int _filterDays = 1;

    public AlarmViewModel(IAlarmService alarmSvc, AlarmRepository repo)
    {
        Title = "报警";
        _alarmSvc = alarmSvc;
        _repo = repo;

        // 订阅报警变化,刷新当前报警列表
        _alarmSvc.AlarmChanged += OnAlarmChanged;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (s, e) => RefreshActive();
        timer.Start();

        // 启动时加载一次历史
        LoadHistory();
    }

    private void OnAlarmChanged(AlarmRecord rec) => RefreshActive();

    private void RefreshActive()
    {
        var list = _alarmSvc.GetActiveAlarms();
        if (list.Count != ActiveAlarms.Count || !ActiveAlarms.SequenceEqual(list))
        {
            ActiveAlarms.Clear();
            foreach (var a in list) ActiveAlarms.Add(a);
        }
    }

    [RelayCommand]
    private void Ack()
    {
        _alarmSvc.Ack();
        RefreshActive();
    }

    [RelayCommand]
    private void LoadHistory()
    {
        try
        {
            var from = DateTime.Now.AddDays(-FilterDays);
            var rows = _repo.Query(from: from, limit: 500);
            HistoryAlarms.Clear();
            foreach (var r in rows) HistoryAlarms.Add(r);
        }
        catch { /* MySQL 不可用时静默 */ }
    }
}
