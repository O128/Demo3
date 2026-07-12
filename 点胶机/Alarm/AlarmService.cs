using Serilog;
using 点胶机.Core.Enums;
using 点胶机.Core.Events;
using 点胶机.Core.Interfaces;

namespace 点胶机.Alarm;

/// <summary>
/// 报警服务 —— 管理报警触发/确认/历史
/// 对齐 AutoStudio.Service.AlarmService
/// 由 Task_AlarmMonitor 周期调用 AddAlarm
/// </summary>
public sealed class AlarmService : IAlarmService
{
    private readonly IEventBus _eventBus;
    private readonly Data.AlarmRepository? _repo;
    private readonly object _lock = new();

    /// <summary>当前活跃报警(未确认)</summary>
    private readonly List<AlarmRecord> _active = new();

    /// <summary>报警定义表</summary>
    public static readonly Dictionary<int, (AlarmLevel Level, string Name)> Definitions = new()
    {
        { 0, (AlarmLevel.Alarm_Stop, "急停触发") },
        { 1, (AlarmLevel.Alarm_Stop, "X 轴正限位") },
        { 2, (AlarmLevel.Alarm_Stop, "X 轴负限位") },
        { 3, (AlarmLevel.Alarm_Stop, "Y 轴正限位") },
        { 4, (AlarmLevel.Alarm_Stop, "Y 轴负限位") },
        { 5, (AlarmLevel.Alarm_Stop, "Z 轴正限位") },
        { 6, (AlarmLevel.Alarm_Stop, "Z 轴负限位") },
        { 7, (AlarmLevel.Alarm_Stop, "伺服驱动器故障") },
        { 8, (AlarmLevel.Alarm_Stop, "运动到位超时") },
        { 9, (AlarmLevel.Alarm_Pause, "气压不足") },
        { 10, (AlarmLevel.Alarm_Pause, "点胶阀超时") },
        { 11, (AlarmLevel.Tip, "未就绪启动") },
        { 12, (AlarmLevel.Alarm_Stop, "系统未初始化") },
    };

    /// <summary>已触发但尚未确认的报警的 Id 集合(去重用)</summary>
    private readonly HashSet<int> _pendingIds = new();

    public bool HasActiveAlarm { get; private set; }

    public event Action<AlarmRecord>? AlarmChanged;

    public AlarmService(IEventBus eventBus, Data.AlarmRepository? repo = null)
    {
        _eventBus = eventBus;
        _repo = repo;
    }

    public void AddAlarm(bool condition, int alarmId, AlarmLevel level, string name)
    {
        if (!condition) return;

        lock (_lock)
        {
            // 去重:同一报警未确认时不重复触发
            if (_pendingIds.Contains(alarmId)) return;
            _pendingIds.Add(alarmId);

            var rec = new AlarmRecord(alarmId, name, level, DateTime.Now);
            _active.Add(rec);

            // 停机/暂停级报警 → 设置全局 IsAlarm
            if (level != AlarmLevel.Tip)
            {
                HasActiveAlarm = true;
                Engine.TaskStatic.Instance.IsAlarm = true;
            }
            Engine.TaskStatic.Instance.TodayAlarmCount++;
        }

        Log.Warning("[报警触发] #{Id} [{Level}] {Name}", alarmId, level, name);

        // 写报警历史表
        try { _repo?.InsertStart(alarmId, name, level.ToString(), DateTime.Now); }
        catch (Exception ex) { Log.Error(ex, "写报警历史失败"); }

        // 发事件:UI(AlarmView/Dialog)订阅
        _eventBus.Publish(new AlarmEvent
        {
            AlarmId = alarmId,
            Name = name,
            Level = level,
            StartTime = DateTime.Now,
            IsActive = true
        });
        AlarmChanged?.Invoke(new AlarmRecord(alarmId, name, level, DateTime.Now));
    }

    /// <summary>确认当前所有活跃报警</summary>
    public void Ack()
    {
        List<AlarmRecord> snapshot;
        lock (_lock)
        {
            if (_active.Count == 0) return;
            snapshot = _active.ToList();
            _active.Clear();
            _pendingIds.Clear();
            HasActiveAlarm = false;
        }

        Engine.TaskStatic.Instance.IsAlarm = false;

        foreach (var rec in snapshot)
        {
            Log.Information("[报警确认] #{Id} {Name}, 持续 {Sec:F1}s",
                rec.AlarmId, rec.Name, (DateTime.Now - rec.StartTime).TotalSeconds);
            try { _repo?.UpdateEnd(rec.AlarmId, rec.StartTime, DateTime.Now); }
            catch (Exception ex) { Log.Error(ex, "更新报警结束记录失败"); }
            _eventBus.Publish(new AlarmEvent
            {
                AlarmId = rec.AlarmId,
                Name = rec.Name,
                Level = rec.Level,
                StartTime = rec.StartTime,
                IsActive = false
            });
        }
    }

    /// <summary>获取当前活跃报警列表(UI 用)</summary>
    public List<AlarmRecord> GetActiveAlarms()
    {
        lock (_lock) return _active.ToList();
    }
}
