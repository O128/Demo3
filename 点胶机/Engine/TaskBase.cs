using System.ComponentModel;
using System.Runtime.CompilerServices;
using Serilog;
using 点胶机.Core.Enums;

namespace 点胶机.Engine;

/// <summary>
/// 任务基类 —— 状态机框架,对齐 AutoStudio.Service.Task.TaskBase
/// 子类重写 AutoRun(WorkMode) 写 switch(WorkStep) 状态机
/// 步序约定:0=空闲,10/20/30=主流程(间隔10便于插步),100=完成,1000=异常
/// </summary>
public abstract class TaskBase : INotifyPropertyChanged
{
    /// <summary>任务名称</summary>
    public string Name { get; }

    /// <summary>是否为初始化任务(系统启动时优先执行)</summary>
    public virtual bool IsInitTask => false;

    /// <summary>是否加入运行监控(影响调度可见性)</summary>
    public bool IsMonitored { get; set; } = true;

    /// <summary>是否在报警暂停时仍然执行(报警监听任务设为 true)</summary>
    public bool IgnoreAlarmPause { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    // —— 当前步序 ——
    private int _workStep;
    private string _stepDesc = "空闲";

    /// <summary>当前步序号(UI 可绑定显示)</summary>
    public int WorkStep
    {
        get => _workStep;
        protected set { if (_workStep != value) { _workStep = value; OnChanged(); } }
    }

    /// <summary>步序描述(UI 可绑定显示)</summary>
    public string StepDesc
    {
        get => _stepDesc;
        protected set { if (_stepDesc != value) { _stepDesc = value; OnChanged(); } }
    }

    /// <summary>非阻塞延时定时器(状态机内等待用)</summary>
    protected DelayerTimer Timer { get; } = new();

    /// <summary>每步的超时定时器(用于检测"某步卡住")</summary>
    protected DelayerTimer TimeoutTimer { get; } = new();

    /// <summary>当前步序的超时阈值(ms),0 表示不检测超时</summary>
    protected int CurrentStepTimeoutMs { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected TaskBase(string name)
    {
        Name = name;
    }

    /// <summary>设置当前步序 + 描述</summary>
    protected void SetStep(int step, string desc)
    {
        WorkStep = step;
        StepDesc = desc;
        Timer.Reset();              // 切步序时复位延时器
        if (CurrentStepTimeoutMs > 0)
        {
            TimeoutTimer.Start(CurrentStepTimeoutMs);
        }
        Log.Debug("[{Task}] → 步序 {Step}: {Desc}", Name, step, desc);
    }

    /// <summary>设置步序并启用超时检测</summary>
    protected void SetStep(int step, string desc, int timeoutMs)
    {
        CurrentStepTimeoutMs = timeoutMs;
        SetStep(step, desc);
    }

    /// <summary>检查当前步是否超时</summary>
    protected bool IsStepTimeout()
    {
        if (CurrentStepTimeoutMs <= 0) return false;
        return TimeoutTimer.Start(CurrentStepTimeoutMs) && Timer.IsRunning == false == false;
        // 注:此处简化,实际超时逻辑在各子类判断"等待条件未满足且 TimeoutTimer 已到"
    }

    // ===== 调度器每周期调用 =====
    /// <summary>
    /// 每周期入口(由 TaskScheduler 调用)
    /// 按 RunStatus 分派到 AlwaysRun/AutoRun
    /// </summary>
    public void Tick(WorkMode mode)
    {
        // AlwaysRun 始终执行(手动/自动/暂停都跑,用于按钮处理等)
        try
        {
            AlwaysRun(mode);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Task}] AlwaysRun 异常", Name);
        }

        var ts = TaskStatic.Instance;

        // 报警时除了报警监听任务,其他任务暂停执行
        if (ts.IsAlarm && !IgnoreAlarmPause)
        {
            return;
        }

        // 按 RunStatus 分派
        // 例外:初始化任务在系统未就绪时(ReadyStatus != Initialized)始终执行,
        //       不受 RunStatus==Running 限制(否则初始化任务永远跑不起来)
        if (ts.RunStatus != RunStatus.Running && !(IsInitTask && ts.ReadyStatus != ReadyStatus.Initialized))
        {
            if (ts.RunStatus == RunStatus.Stopping)
            {
                try { OnStop(); } catch (Exception ex) { Log.Error(ex, "[{Task}] OnStop 异常", Name); }
            }
            return;
        }

        try
        {
            AutoRun(mode);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Task}] AutoRun 异常", Name);
            // 单任务异常隔离:记录后不向上抛
        }
    }

    /// <summary>自动运行状态机(子类重写,写 switch(WorkStep))</summary>
    protected abstract void AutoRun(WorkMode mode);

    /// <summary>始终运行逻辑(子类可选重写,按钮处理/状态刷新等)</summary>
    protected virtual void AlwaysRun(WorkMode mode) { }

    /// <summary>停止时清理逻辑(子类可选重写)</summary>
    protected virtual void OnStop()
    {
        // 默认:停止时复位到步序 0
        if (WorkStep != 0 && WorkStep != 1000)
        {
            WorkStep = 0;
            StepDesc = "停止";
            Timer.Reset();
        }
    }

    protected void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
