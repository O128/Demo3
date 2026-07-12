using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using 点胶机.Core.Enums;
using 点胶机.Hardware;

namespace 点胶机.ViewModels;

/// <summary>IO 监控页 —— 所有输入/输出指示灯(从软 PLC DB2 读取)</summary>
public partial class IoViewModel : ViewModelBase
{
    private readonly HardwareManager _hw;

    /// <summary>IO 指示项</summary>
    public ObservableCollection<IoItem> Inputs { get; } = new();
    public ObservableCollection<IoItem> Outputs { get; } = new();

    public IoViewModel(HardwareManager hw)
    {
        Title = "IO 监控";
        _hw = hw;

        // 初始化 IO 列表(输入)
        Inputs.Add(new IoItem(0, "急停按钮"));
        Inputs.Add(new IoItem(1, "启动按钮"));
        Inputs.Add(new IoItem(2, "停止按钮"));
        Inputs.Add(new IoItem(3, "暂停按钮"));
        Inputs.Add(new IoItem(4, "复位按钮"));
        Inputs.Add(new IoItem(5, "有工件"));
        Inputs.Add(new IoItem(6, "X 原点"));
        Inputs.Add(new IoItem(7, "Y 原点"));
        Inputs.Add(new IoItem(8, "Z 原点"));
        // 输出
        Outputs.Add(new IoItem(0, "红灯"));
        Outputs.Add(new IoItem(1, "黄灯"));
        Outputs.Add(new IoItem(2, "绿灯"));
        Outputs.Add(new IoItem(3, "蜂鸣器"));
        Outputs.Add(new IoItem(4, "点胶阀"));
        Outputs.Add(new IoItem(5, "夹具"));

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
public class IoItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public int Index { get; }
    public string Name { get; }

    private bool _active;
    public bool Active
    {
        get => _active;
        set => SetProperty(ref _active, value);
    }

    public IoItem(int index, string name) { Index = index; Name = name; }
}
