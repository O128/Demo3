using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using 点胶机.Core.Config;
using 点胶机.Core.Enums;
using 点胶机.Engine;
using 点胶机.Hardware;
using 点胶机.Hardware.Devices;

namespace 点胶机.ViewModels;

/// <summary>设置页 —— 工作模式切换 + 急停/气压故障演示</summary>
public partial class SettingViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly HardwareManager _hw;

    [ObservableProperty] private int _workModeIndex = 1;   // 0=Manual,1=Auto,2=EmptyRun
    [ObservableProperty] private string _dbStatus = "";
    [ObservableProperty] private double _pressureFaultChance;

    public SettingViewModel(AppConfig config, HardwareManager hw)
    {
        Title = "设置";
        _config = config;
        _hw = hw;
        DbStatus = $"Server={config.Database.ConnectionString}";
        WorkModeIndex = (int)TaskStatic.Instance.WorkMode;
    }

    partial void OnWorkModeIndexChanged(int value)
    {
        TaskStatic.Instance.WorkMode = (WorkMode)value;
    }

    [RelayCommand]
    private void TriggerEstop()
    {
        // 模拟按下急停按钮(写软 PLC 输入)
        _hw.Plc.SetInput(IoIndex.In_Estop, true);
    }

    [RelayCommand]
    private void TriggerPressureFault()
    {
        // 设置压力传感器故障概率,演示气压低报警
        if (_hw.Pressure is PressureSensorSimulator ps)
        {
            ps.SetFaultChance(0.8);
        }
    }

    [RelayCommand]
    private void ClearPressureFault()
    {
        if (_hw.Pressure is PressureSensorSimulator ps)
        {
            ps.SetFaultChance(0);
        }
    }
}
