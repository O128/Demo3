using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using 点胶机.Core.Interfaces;
using 点胶机.Engine;
using 点胶机.Hardware;

namespace 点胶机.ViewModels;

/// <summary>主页 —— OEE 看板 + 三轴实时位置</summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly HardwareManager _hw;

    [ObservableProperty] private int _todayYield;
    [ObservableProperty] private string _okRate = "100%";
    [ObservableProperty] private string _runTime = "0 分钟";
    [ObservableProperty] private int _alarmCount;
    [ObservableProperty] private double _posX;
    [ObservableProperty] private double _posY;
    [ObservableProperty] private double _posZ;
    [ObservableProperty] private bool _xHomed;
    [ObservableProperty] private bool _yHomed;
    [ObservableProperty] private bool _zHomed;

    public HomeViewModel(HardwareManager hw)
    {
        Title = "主页";
        _hw = hw;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (s, e) => Refresh();
        timer.Start();
    }

    private void Refresh()
    {
        var ts = TaskStatic.Instance;
        TodayYield = ts.TodayYield;
        AlarmCount = ts.TodayAlarmCount;
        RunTime = $"{(int)(ts.RunSeconds / 60)} 分钟";

        PosX = _hw.Motion.GetPosition(Core.Enums.AxisId.X);
        PosY = _hw.Motion.GetPosition(Core.Enums.AxisId.Y);
        PosZ = _hw.Motion.GetPosition(Core.Enums.AxisId.Z);
        XHomed = _hw.Motion.IsHomed(Core.Enums.AxisId.X);
        YHomed = _hw.Motion.IsHomed(Core.Enums.AxisId.Y);
        ZHomed = _hw.Motion.IsHomed(Core.Enums.AxisId.Z);
    }
}
