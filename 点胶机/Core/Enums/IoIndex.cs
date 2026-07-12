namespace 点胶机.Core.Enums;

/// <summary>
/// IO 点位索引(对应软 PLC 的 DB2 Input/Output 数组下标)
/// </summary>
public static class IoIndex
{
    // ===== Input 输入(传感器)=====
    public const int In_Estop = 0;          // 急停按钮
    public const int In_StartButton = 1;    // 启动按钮
    public const int In_StopButton = 2;      // 停止按钮
    public const int In_PauseButton = 3;     // 暂停按钮
    public const int In_ResetButton = 4;     // 复位按钮
    public const int In_HasWorkpiece = 5;    // 有工件(夹具到位)
    public const int In_XHome = 6;           // X 原点
    public const int In_YHome = 7;           // Y 原点
    public const int In_ZHome = 8;           // Z 原点
    public const int In_XLimitPos = 9;       // X 正限位
    public const int In_XLimitNeg = 10;      // X 负限位
    public const int In_YLimitPos = 11;      // Y 正限位
    public const int In_YLimitNeg = 12;      // Y 负限位
    public const int In_ZLimitPos = 13;      // Z 正限位
    public const int In_ZLimitNeg = 14;      // Z 负限位

    // ===== Output 输出(执行件)=====
    public const int Out_RedLamp = 0;        // 红灯
    public const int Out_YellowLamp = 1;     // 黄灯
    public const int Out_GreenLamp = 2;      // 绿灯
    public const int Out_Buzzer = 3;         // 蜂鸣器
    public const int Out_GlueValve = 4;      // 点胶阀
    public const int Out_Clamp = 5;          // 夹具气缸
}
