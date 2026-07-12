using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using 点胶机.Core.Enums;
using 点胶机.Engine;
using 点胶机.Tasks;

namespace 点胶机.ViewModels;

/// <summary>自动页 —— 流程图显示步序(高亮当前)+ 顶端状态条联动运行状态</summary>
public partial class AutoViewModel : ViewModelBase
{
    private readonly Task_Dispensing _task;

    /// <summary>流程图节点(固定 11 步)</summary>
    public ObservableCollection<FlowNode> Nodes { get; }

    [ObservableProperty] private string _stepDesc = "空闲";
    [ObservableProperty] private int _workStep;
    [ObservableProperty] private int _todayYield;
    [ObservableProperty] private string _runStatusText = "停止";
    [ObservableProperty] private string _runStatusColor = "#888888";   // 顶端状态条颜色

    /// <summary>当前流程节点索引(用于高亮,-1 表示未运行/待机)</summary>
    [ObservableProperty] private int _currentNodeIndex = -1;

    public AutoViewModel(Task_Dispensing task)
    {
        Title = "自动";
        _task = task;

        // 流程节点(显示名,对应的步序范围)
        // 发起步+等待步合并为一个节点
        Nodes = new ObservableCollection<FlowNode>
        {
            new(0, "待机", "等待启动"),
            new(1, "Z 升上位", "步序 10/11"),
            new(2, "XY 到拍照位", "步序 20/21"),
            new(3, "视觉定位", "步序 30(仿真延时)"),
            new(4, "XY 到点胶起点", "步序 40/41"),
            new(5, "Z 降到点胶高度", "步序 50/51"),
            new(6, "执行点胶轨迹", "步序 60/61"),
            new(7, "Z 上升", "步序 70/71"),
            new(8, "产量计数", "步序 80"),
            new(9, "回 Home", "步序 90/91"),
            new(10, "本片完成", "步序 100"),
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += (s, e) => Refresh();
        timer.Start();
    }

    private void Refresh()
    {
        var ts = TaskStatic.Instance;
        StepDesc = _task.StepDesc;
        WorkStep = _task.WorkStep;
        TodayYield = ts.TodayYield;

        RunStatusText = ts.RunStatus switch
        {
            RunStatus.Running => "运行中",
            RunStatus.Paused => "暂停",
            _ => "停止"
        };
        // 顶端状态条颜色联动:运行绿 / 暂停橙 / 停止灰 / 报警红
        if (ts.IsAlarm)
            RunStatusColor = "#E53935";   // 红
        else
            RunStatusColor = ts.RunStatus switch
            {
                RunStatus.Running => "#43A047",   // 绿
                RunStatus.Paused => "#FB8C00",    // 橙
                _ => "#757575"                    // 灰
            };

        // 步序 → 流程节点索引映射(发起步+等待步合并)
        int idx = WorkStep switch
        {
            0 => 0,
            10 or 11 => 1,
            20 or 21 => 2,
            30 => 3,
            40 or 41 => 4,
            50 or 51 => 5,
            60 or 61 => 6,
            70 or 71 => 7,
            80 => 8,
            90 or 91 => 9,
            100 => 10,
            _ => -1   // 1000 异常等
        };

        // 仅在运行中才高亮;停止/待机不高亮
        bool highlight = ts.RunStatus == RunStatus.Running || (ts.RunStatus == RunStatus.Stopping && WorkStep == 0);

        if (CurrentNodeIndex != idx)
        {
            // 先清除旧高亮
            if (CurrentNodeIndex >= 0 && CurrentNodeIndex < Nodes.Count)
                Nodes[CurrentNodeIndex].State = FlowNodeState.Pending;
            CurrentNodeIndex = idx;
        }

        // 更新各节点状态:当前=Active,之前的=Done,之后的=Pending
        for (int i = 0; i < Nodes.Count; i++)
        {
            if (!highlight)
            {
                Nodes[i].State = FlowNodeState.Pending;
            }
            else if (i == idx)
            {
                Nodes[i].State = FlowNodeState.Active;
            }
            else if (i < idx)
            {
                Nodes[i].State = FlowNodeState.Done;
            }
            else
            {
                Nodes[i].State = FlowNodeState.Pending;
            }
        }
    }

    [RelayCommand] private void Start() => TaskStatic.Instance.StartButton = true;
    [RelayCommand] private void Pause() => TaskStatic.Instance.PauseButton = true;
    [RelayCommand] private void StopRun() => TaskStatic.Instance.StopButton = true;
    [RelayCommand] private void Reset() => TaskStatic.Instance.ResetButton = true;
}

/// <summary>流程图节点</summary>
public class FlowNode : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public int Index { get; }
    public string Title { get; }
    public string Subtitle { get; }

    private FlowNodeState _state = FlowNodeState.Pending;
    /// <summary>节点状态(决定颜色)</summary>
    public FlowNodeState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    public FlowNode(int index, string title, string subtitle)
    {
        Index = index; Title = title; Subtitle = subtitle;
    }
}

/// <summary>节点状态枚举</summary>
public enum FlowNodeState
{
    /// <summary>未执行(灰)</summary>
    Pending,
    /// <summary>已完成(淡绿)</summary>
    Done,
    /// <summary>当前执行中(高亮蓝/亮色 + 边框)</summary>
    Active
}
