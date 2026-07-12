using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;
using 点胶机.Hardware;
using 点胶机.Services.Toast;

namespace 点胶机.ViewModels;

/// <summary>手动页 —— 轴调试(JOG/回零/定位)+ 胶阀测试</summary>
public partial class ManualViewModel : ViewModelBase
{
    private readonly HardwareManager _hw;
    private readonly IToastService _toast;

    [ObservableProperty] private AxisId _selectedAxis = AxisId.X;
    [ObservableProperty] private double _posX;
    [ObservableProperty] private double _posY;
    [ObservableProperty] private double _posZ;
    [ObservableProperty] private double _targetPos;
    [ObservableProperty] private double _moveSpeed = 50;
    [ObservableProperty] private bool _servoOnX, _servoOnY, _servoOnZ;

    /// <summary>本页面是否被禁用(自动模式/运行中)——用于显示横幅 + 禁用操作按钮</summary>
    [ObservableProperty] private bool _isDisabled;
    [ObservableProperty] private string _disabledReason = "";

    /// <summary>轴选项(供下拉框绑定)</summary>
    public AxisId[] AxisOptions { get; } = { AxisId.X, AxisId.Y, AxisId.Z };

    public ManualViewModel(HardwareManager hw, IToastService toast)
    {
        Title = "手动";
        _hw = hw;
        _toast = toast;
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

        // 镜像 ShellViewModel 的禁用逻辑(自动模式或运行中禁用手动)
        var ts = Engine.TaskStatic.Instance;
        bool disabled = ts.WorkMode == WorkMode.Auto || ts.RunStatus == RunStatus.Running;
        IsDisabled = disabled;
        DisabledReason = ts.RunStatus == RunStatus.Running
            ? "⚠ 系统运行中,手动操作已禁用(请先按停止)"
            : ts.WorkMode == WorkMode.Auto ? "⚠ 当前为自动模式,手动操作已禁用(请在设置页切换到手动模式)" : "";
    }

    [RelayCommand]
    private void ServoOn()
    {
        _hw.Motion.ServoOn(SelectedAxis);
        _toast.Info("手动", $"{SelectedAxis} 轴伺服上电");
    }
    [RelayCommand]
    private void ServoOff()
    {
        _hw.Motion.ServoOff(SelectedAxis);
        _toast.Info("手动", $"{SelectedAxis} 轴伺服下电");
    }
    [RelayCommand]
    private void Home()
    {
        if (!_hw.Motion.IsServoOn(SelectedAxis)) { _toast.Warning("手动", $"{SelectedAxis} 轴未上电,无法回零"); return; }
        _hw.Motion.Home(SelectedAxis);
        _toast.Info("手动", $"{SelectedAxis} 轴开始回零");
    }
    [RelayCommand]
    private void MoveAbs()
    {
        bool ok = _hw.Motion.MoveAbsolute(SelectedAxis, TargetPos, MoveSpeed);
        if (ok) _toast.Info("手动", $"{SelectedAxis} 轴绝对定位 → {TargetPos:F2}mm");
        else _toast.Warning("手动", $"{SelectedAxis} 轴定位失败(未上电或超软限位)");
    }
    [RelayCommand]
    private void Stop() => _hw.Motion.Stop(SelectedAxis);

    // JOG:点一下持续运动,点"停止JOG"停
    [RelayCommand]
    private void JogPos()
    {
        if (!_hw.Motion.IsServoOn(SelectedAxis)) { _toast.Warning("手动", $"{SelectedAxis} 轴未上电,无法 JOG"); return; }
        _hw.Motion.JogStart(SelectedAxis, 1, MoveSpeed);
        _toast.Info("手动", $"{SelectedAxis} 轴 JOG+ (速度 {MoveSpeed:F0}),点'停止JOG'停止");
    }
    [RelayCommand]
    private void JogNeg()
    {
        if (!_hw.Motion.IsServoOn(SelectedAxis)) { _toast.Warning("手动", $"{SelectedAxis} 轴未上电,无法 JOG"); return; }
        _hw.Motion.JogStart(SelectedAxis, -1, MoveSpeed);
        _toast.Info("手动", $"{SelectedAxis} 轴 JOG- (速度 {MoveSpeed:F0}),点'停止JOG'停止");
    }
    [RelayCommand]
    private void JogStop()
    {
        _hw.Motion.JogStop(SelectedAxis);
    }

    // 胶阀测试
    [RelayCommand] private void GlueOpen() { _hw.GlueValve.Open(); _hw.Plc.WriteOutput(IoIndex.Out_GlueValve, true); _toast.Info("手动", "点胶阀打开"); }
    [RelayCommand] private void GlueClose() { _hw.GlueValve.Close(); _hw.Plc.WriteOutput(IoIndex.Out_GlueValve, false); _toast.Info("手动", "点胶阀关闭"); }
}
