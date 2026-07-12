using 点胶机.Core.Enums;

namespace 点胶机.Hardware.Plc;

/// <summary>
/// 软 PLC 数据区模型 —— 按西门子 S7-1500 的 DB 块组织方式建模
/// 这样将来切换真实 S7Net 时,上层接口不变,仅替换 IPlcHardware 实现
///
/// 数据区划分(对齐方案):
///   DB1 轴控制/状态 (X/Y/Z 各一组)
///   DB2 IO           (Input[256]/Output[256])
///   DB3 主状态       (按位映射)
///   DB4 主命令       (按位映射)
///   DB5 报警字       (AlarmWord[64])
///   DB6 配方         (点胶速度/胶量等)
/// </summary>
public sealed class S7DataBlock
{
    // ===== DB1 轴(每组含控制字 + 状态字)=====
    public AxisBlock[] Axes { get; } = new AxisBlock[3];

    // ===== DB2 IO =====
    public bool[] Inputs { get; } = new bool[256];
    public bool[] Outputs { get; } = new bool[256];

    // ===== DB3 主状态(按位)=====
    public bool[] MainStatus { get; } = new bool[64];

    // ===== DB4 主命令(按位)=====
    public bool[] MainCommand { get; } = new bool[64];

    // ===== DB5 报警字(按位)=====
    public bool[] AlarmWord { get; } = new bool[64];

    // ===== DB6 配方 =====
    public RecipeBlock Recipe { get; } = new();

    // ===== DB3 主状态位索引 =====
    public static class MainStatusBit
    {
        public const int Manual = 0;
        public const int Auto = 1;
        public const int Running = 2;
        public const int Initialized = 3;
        public const int Initializing = 4;
        public const int Paused = 5;
        public const int AlarmA = 10;   // 停机级
        public const int AlarmB = 11;   // 暂停级
        public const int AlarmC = 12;   // 提示级
        public const int AlarmD = 13;
        public const int Homed = 20;    // 全部回零完成
    }

    // ===== DB4 主命令位索引 =====
    public static class MainCmdBit
    {
        public const int Start = 0;
        public const int Stop = 1;
        public const int Pause = 2;
        public const int Reset = 3;
        public const int Initialize = 4;
    }

    // ===== DB5 报警位索引 =====
    public static class AlarmBit
    {
        public const int Estop = 0;          // 急停
        public const int X_LimitPos = 1;     // X 正限位
        public const int X_LimitNeg = 2;
        public const int Y_LimitPos = 3;
        public const int Y_LimitNeg = 4;
        public const int Z_LimitPos = 5;
        public const int Z_LimitNeg = 6;
        public const int AxisFault = 7;      // 轴故障
        public const int MotionTimeout = 8;  // 运动超时
        public const int PressureLow = 9;    // 气压低
        public const int GlueTimeout = 10;   // 胶阀超时
        public const int NotReady = 11;      // 未就绪启动
    }

    public S7DataBlock()
    {
        for (int i = 0; i < Axes.Length; i++)
            Axes[i] = new AxisBlock((AxisId)i);
    }
}

/// <summary>
/// 单个轴的数据块(DB1 每轴一组):控制字 + 状态字 + 实参
/// 对齐 AutoStudio 的 FSG_AxisCMD / FSG_AxisStatus
/// </summary>
public class AxisBlock
{
    public AxisId Axis { get; }

    public AxisBlock(AxisId axis) { Axis = axis; }

    // ===== 控制字(DB1.CMD,上位机写)=====
    public bool ServoOnCmd;       // 伺服上电
    public bool HomeCmd;          // 回零
    public bool MoveAbsCmd;       // 绝对定位
    public bool MoveRelCmd;       // 相对定位
    public bool JogFwdCmd;        // 正转点动
    public bool JogBwdCmd;        // 反转点动
    public bool StopCmd;          // 停止
    public bool ResetCmd;         // 复位

    // ===== 运动参数 =====
    public double TargetPos;      // 目标位置(mm)
    public double MoveVel;        // 运动速度(mm/s)
    public double JogVel;         // 点动速度
    public double HomeVel;        // 回零速度

    // ===== 轴参数(初始化时下发)=====
    public double PulseUnit = 1000;          // 脉冲当量(脉冲/mm)
    public double SoftLimitPos = 300;        // 正软限位(mm)
    public double SoftLimitNeg = -10;        // 负软限位(mm)
    public double RunSpeed = 100;
    public double HomeSpeed = 20;

    // ===== 状态字(DB1.STATUS,PLC 上传)=====
    public bool ServoOn;          // 伺服已上电
    public bool Homed;            // 已回零
    public bool Moving;           // 运动中
    public bool InPos;            // 到位
    public bool Fault;            // 故障
    public bool LimitPos;         // 正限位触发
    public bool LimitNeg;         // 负限位触发

    // ===== 实际值 =====
    public double ActPos;         // 实际位置(mm)
    public double ActVel;         // 实际速度(mm/s)

    // ===== 内部仿真变量 =====
    public double _moveStartPos;  // 运动起始位置
    public int _moveDir;          // 运动方向 +1/-1/0
}

/// <summary>配方数据块(DB6)</summary>
public class RecipeBlock
{
    public string RecipeName { get; set; } = "Default";
    public double DispenseSpeed { get; set; } = 50;     // 点胶速度 mm/s
    public double GlueFlow { get; set; } = 0.5;          // 胶量 mg/ms
    public int PointCount { get; set; }                  // 轨迹点数
}
