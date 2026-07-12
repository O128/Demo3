using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using 点胶机.Core.Enums;
using 点胶机.Hardware;

namespace 点胶机.ViewModels;

/// <summary>IO 监控页 —— 所有输入/输出指示灯(从软 PLC DB2 读取)
/// 输入项有"模拟置1"按钮(写软 PLC Input),输出项有"强制置1/清0"按钮(写 Output)</summary>
public partial class IoViewModel : ViewModelBase
{
    private readonly HardwareManager _hw;

    public ObservableCollection<IoItem> Inputs { get; } = new();
    public ObservableCollection<IoItem> Outputs { get; } = new();

    public IoViewModel(HardwareManager hw)
    {
        Title = "IO 监控";
        _hw = hw;

        // 输入项(带"置1/置0"按钮 —— 模拟传感器信号)
        // 注意:急停为低电平有效(常态高=true,置0=按下触发)
        Inputs.Add(new IoItem(0, "急停按钮", isOutput: false, i => hw.Plc.SetInput(i, true), i => hw.Plc.SetInput(i, false)));
        Inputs.Add(new IoItem(1, "启动按钮", isOutput: false, i => hw.Plc.SetInput(i, true), i => hw.Plc.SetInput(i, false)));
        Inputs.Add(new IoItem(2, "停止按钮", isOutput: false, i => hw.Plc.SetInput(i, true), i => hw.Plc.SetInput(i, false)));
        Inputs.Add(new IoItem(3, "暂停按钮", isOutput: false, i => hw.Plc.SetInput(i, true), i => hw.Plc.SetInput(i, false)));
        Inputs.Add(new IoItem(4, "复位按钮", isOutput: false, i => hw.Plc.SetInput(i, true), i => hw.Plc.SetInput(i, false)));
        Inputs.Add(new IoItem(5, "有工件", isOutput: false, i => hw.Plc.SetInput(i, true), i => hw.Plc.SetInput(i, false)));
        Inputs.Add(new IoItem(6, "X 原点", isOutput: false, i => hw.Plc.SetInput(i, true), i => hw.Plc.SetInput(i, false)));
        Inputs.Add(new IoItem(7, "Y 原点", isOutput: false, i => hw.Plc.SetInput(i, true), i => hw.Plc.SetInput(i, false)));
        Inputs.Add(new IoItem(8, "Z 原点", isOutput: false, i => hw.Plc.SetInput(i, true), i => hw.Plc.SetInput(i, false)));

        // 输出项(带"强制置1/清0"按钮 —— 直接写 PLC Output)
        Outputs.Add(new IoItem(0, "红灯", isOutput: true, o => hw.Plc.WriteOutput(o, true), o => hw.Plc.WriteOutput(o, false)));
        Outputs.Add(new IoItem(1, "黄灯", isOutput: true, o => hw.Plc.WriteOutput(o, true), o => hw.Plc.WriteOutput(o, false)));
        Outputs.Add(new IoItem(2, "绿灯", isOutput: true, o => hw.Plc.WriteOutput(o, true), o => hw.Plc.WriteOutput(o, false)));
        Outputs.Add(new IoItem(3, "蜂鸣器", isOutput: true, o => hw.Plc.WriteOutput(o, true), o => hw.Plc.WriteOutput(o, false)));
        Outputs.Add(new IoItem(4, "点胶阀", isOutput: true, o => hw.Plc.WriteOutput(o, true), o => hw.Plc.WriteOutput(o, false)));
        Outputs.Add(new IoItem(5, "夹具", isOutput: true, o => hw.Plc.WriteOutput(o, true), o => hw.Plc.WriteOutput(o, false)));

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (s, e) => Refresh();
        timer.Start();
    }

    private void Refresh()
    {
        foreach (var i in Inputs) i.Active = _hw.Plc.ReadInput(i.Index);
        foreach (var o in Outputs) o.Active = _hw.Plc.ReadOutput(o.Index);
    }
}

/// <summary>IO 指示项</summary>
public class IoItem : ObservableObject
{
    public int Index { get; }
    public string Name { get; }
    /// <summary>是否为输出项(决定显示哪些按钮)</summary>
    public bool IsOutput { get; }

    private bool _active;
    /// <summary>当前状态(点亮/熄灭)</summary>
    public bool Active
    {
        get => _active;
        set => SetProperty(ref _active, value);
    }

    /// <summary>置1命令(输入=模拟置1,输出=强制置1)</summary>
    public ICommand SetOnCommand { get; }
    /// <summary>清0命令(仅输出项有)</summary>
    public ICommand? SetOffCommand { get; }

    public IoItem(int index, string name, bool isOutput, Action<int> setOn, Action<int>? setOff = null)
    {
        Index = index; Name = name; IsOutput = isOutput;
        SetOnCommand = new RelayCommand(() => setOn(index));
        SetOffCommand = setOff is null ? null : new RelayCommand(() => setOff(index));
    }
}
