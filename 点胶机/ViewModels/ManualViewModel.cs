using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;
using 点胶机.Hardware;

namespace 点胶机.ViewModels;

/// <summary>手动页 —— 轴调试(JOG/回零/定位)+ 胶阀测试</summary>
public partial class ManualViewModel : ViewModelBase
{
    private readonly HardwareManager _hw;

    [ObservableProperty] private AxisId _selectedAxis = AxisId.X;
    [ObservableProperty] private double _posX;
    [ObservableProperty] private double _posY;
    [ObservableProperty] private double _posZ;
    [ObservableProperty] private double _targetPos;
    [ObservableProperty] private double _moveSpeed = 50;
    [ObservableProperty] private bool _servoOnX, _servoOnY, _servoOnZ;

    /// <summary>轴选项(供下拉框绑定)</summary>
    public AxisId[] AxisOptions { get; } = { AxisId.X, AxisId.Y, AxisId.Z };

    public ManualViewModel(HardwareManager hw)
    {
        Title = "手动";
        _hw = hw;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (s, e) => Refresh();
        timer.Start();
    }

    private void Refresh()
    {
        PosX = _hw.Motion.GetPosition(AxisId.X);
        PosY = _hw.Motion.GetPosition(AxisId.Y);
        PosZ = _hw.Motion.GetPosition(AxisId.Z);
        ServoOnX = _hw.Motion.IsServoOn(AxisId.X);
        ServoOnY = _hw.Motion.IsServoOn(AxisId.Y);
        ServoOnZ = _hw.Motion.IsServoOn(AxisId.Z);
    }

    [RelayCommand] private void ServoOn() => _hw.Motion.ServoOn(SelectedAxis);
    [RelayCommand] private void ServoOff() => _hw.Motion.ServoOff(SelectedAxis);
    [RelayCommand] private void Home() => _hw.Motion.Home(SelectedAxis);
    [RelayCommand] private void MoveAbs() => _hw.Motion.MoveAbsolute(SelectedAxis, TargetPos, MoveSpeed);
    [RelayCommand] private void Stop() => _hw.Motion.Stop(SelectedAxis);

    // JOG:按下开始点动,松开停止(WPF 用 PreviewMouseDown/Up 触发)
    [RelayCommand] private void JogPos() => _hw.Motion.JogStart(SelectedAxis, 1, MoveSpeed);
    [RelayCommand] private void JogNeg() => _hw.Motion.JogStart(SelectedAxis, -1, MoveSpeed);
    [RelayCommand] private void JogStop() => _hw.Motion.JogStop(SelectedAxis);

    // 胶阀测试
    [RelayCommand] private void GlueOpen() => _hw.GlueValve.Open();
    [RelayCommand] private void GlueClose() => _hw.GlueValve.Close();
}
