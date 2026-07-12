
# 点胶机自动控制系统(仿真版)设计方案 【定稿】

> 本次修订:UI 采用**启动壳(Shell Window)+ 用户控件(UserControl)** 模式(对齐 AutoStudio 的 AppShell),并新增**独立弹窗画面 NotifyWindow** 专用于提示和警报。视觉为纯延时环节(无图像/算法/偏移,延时后 offset=0 直接下一步)。

## 一、定位与原则

- 单工位桌面型点胶机,纯仿真(无真实硬件)
- 单项目(单 csproj),文件夹+命名空间分层
- 一切业务皆 Task,挂载统一调度器
- 西门子 S7-1500 软 PLC(自建内存仿真,按 S7 数据区结构建模)
- 日志/报警落 MySQL(Serilog + Dapper)

## 二、技术栈

| 层 | 选型 | NuGet |
|----|------|-------|
| 运行时 | .NET 8.0 net8.0-windows | (已有) |
| UI | WPF + MahApps.Metro | `MahApps.Metro` |
| MVVM | CommunityToolkit.Mvvm | `CommunityToolkit.Mvvm` |
| DI/配置 | MS Hosting+DI+Configuration.Json | `Microsoft.Extensions.Hosting`、`.DependencyInjection`、`.Configuration.Json` |
| PLC | 自建软 PLC 仿真 | — |
| 日志 | Serilog → File+MySQL | `Serilog`、`Serilog.Sinks.MySQL`、`Serilog.Sinks.File` |
| DB | MySQL 8.0 + Dapper | `MySqlConnector`、`Dapper` |

## 三、工艺流程(视觉=纯延时)

```
[启动]→Z轴升上位→XY移到拍照位
→【视觉定位=纯延时(默认500ms),结束 offset=0,无异常】
→XY移到点胶起点→Z轴降到点胶高度
→按配方轨迹点胶(逐点移动+胶阀开/关)
→Z轴上升→XY回Home→产量+1→等下一片
```

## 四、单项目分层结构

```
点胶机/
├── 点胶机.csproj / App.xaml(.cs) / appsettings.json / Bootstrapper.cs
├── Core/            Enums/(AxisId X/Y/Z、IoIndex、RunStatus、WorkMode、AlarmLevel)
│                    Interfaces/(IPlcHardware、IMotionCard、IGlueValve、IPressureSensor、IAlarmService、IEventBus、IDialogService)
│                    Events/(MessageEvent、AlarmEvent、StatusChangedEvent)
├── Hardware/        Plc/(S7DataBlock、PlcSimulator)
│                    Motion/(MotionCardBase、MotionCardPlc、AxisParameter)
│                    Devices/(GlueValveSimulator、PressureSensorSimulator)   ← 无相机
│                    HardwareManager.cs
├── Engine/          TaskBase、TaskScheduler、TaskStatic、DelayerTimer、HandEvent
├── Tasks/           Task_SystemInit、Task_Dispensing、Task_AlarmMonitor、Task_IoWatchdog
├── Recipe/          DispenseRecipe、Point(轨迹点)、RecipeManager
├── Alarm/           AlarmService、AlarmDefinition、AlarmRecord
├── Data/            MySqlDb、DbInitializer、LogRepository、AlarmRepository、ProductionRepository、Entities
├── Logging/         SerilogConfig
├── ViewModels/      Shell(MainWindow)、Home、Manual、Auto、Recipe、Alarm、Log、Io、Setting、NotifyDialog
├── Views/           Shell(MainWindow.xaml) + Pages/(均为 UserControl) + Dialogs/NotifyWindow.xaml + Controls/(AxisControl、IoIndicator、StatusBanner)
└── Assets/          Icons/、Themes/
```

## 五、核心子系统

### 5.1 任务调度引擎(对齐 AutoStudio)

- **TaskBase**:状态机基类,子类重写 `AutoRun(WorkMode)` 写 `switch(WorkStep)`;`SetStep(int,desc)`、`Timer`(DelayerTimer 非阻塞延时)
- **TaskScheduler**:N 线程瓜分 Task,每轮 1ms 刷新 IO→遍历 Task 调 `Tick`;单 Task 异常隔离
- **TaskStatic**:`RunStatus`/`ReadyStatus`/`WorkMode`/`IsAlarm`/按钮 `Start/Stop/Pause/Reset/Estop`
- **DelayerTimer**:`Start(ms)` 首次 false、到时 true 自动复位
- **HandEvent**:任务间握手

### 5.2 西门子 S7-1500 软 PLC 仿真

按 S7-1500 数据区建模:DB1 轴控/状态(X/Y/Z)、DB2 IO[256]/[256]、DB3 主状态(按位)、DB4 主命令、DB5 报警字、DB6 配方。
**PlcSimulator** 后台扫描线程(1ms):执行轴运动学(pos+=vel*dt,到目标置 InPos)→检测软限位/急停→同步 IO/状态。暴露 `IPlcHardware`。`IMotionCard` 实现读写 DB1(同 AutoStudio 的 OmronPlcDriver 模式)。`IPlcHardware` 预留真机位。

### 5.3 外设仿真(无相机)

- 点胶阀 `GlueValveSimulator`:Open/Close,开阀累计胶量
- 压力传感器 `PressureSensorSimulator`:返回模拟气压

### 5.4 点胶主流程(Task_Dispensing 状态机)

```
case 0:   等 Start+有板→SetStep(10,"Z轴上升")
case 10:  MoveAbs(Z,上位);到位?→SetStep(20,"移到拍照位")
case 20:  MoveAbs(X,拍照X);MoveAbs(Y,拍照Y);到位?→SetStep(30,"视觉定位")
case 30:  if(Timer.Start(500)){OffsetX=0;OffsetY=0;SetStep(40);}  ← 纯延时,无图像/算法
case 40:  MoveAbs(X,起点X);MoveAbs(Y,起点Y);到位?→SetStep(50,"Z下降")
case 50:  MoveAbs(Z,点胶高度);到位?→SetStep(60,"点胶轨迹")
case 60:  遍历配方 Point(逐点 MoveAbs+胶阀开/关),子步序 61/62...
case 70:  Z上升+胶阀关→SetStep(80,"产量计数")
case 80:  产量+++写 Production→SetStep(90,"回Home")
case 90:  各轴回零位→SetStep(100,"完成")
case 100: 置完成,等下料→SetStep(0)循环
case 1000:异常,等复位
```
每步 `Timer.Start(timeout)` 超时检测→Alarm_Stop。空跑模式(EmptyRun)跳过到位等待。

### 5.5 报警系统

定义:急停/限位/运动超时/气压低/胶阀超时/未就绪,分级 `Alarm_Stop`/`Alarm_Pause`/`Tip`。
`Task_AlarmMonitor` 每周期 AddAlarm;触发→写 MySQL Alarms 表+发 AlarmEvent+联动三色灯蜂鸣+**弹窗(经 IDialogService)**。UI 点 Ack→`AlarmService.Ack(id)`。

### 5.6 日志(Serilog → File+MySQL)

SerilogConfig 双 Sink:File(`logs/yyyyMMdd.txt`)+MySQL(`Logs` 表)。全局异常(Dispatcher/AppDomain/UnobservedTaskException)全写 Serilog。

### 5.7 数据库(MySQL+Dapper,DbInitializer 自动建表)

```sql
Logs(Id PK AI, Timestamp, Level, Module, Message, Exception, MachineName)  INDEX(Timestamp,Level)
Alarms(Id PK AI, AlarmId, AlarmName, Level, StartTime, EndTime, DurationSec, AckTime, AckUser)  INDEX(StartTime,Level)
Production(Id PK AI, StartTime, EndTime, CycleTime, Result, RecipeName, OffsetX, OffsetY, GlueAmount)
```

## 六、UI 设计【本次重点修订:Shell+UserControl 模式 + 独立弹窗画面】

对齐 AutoStudio 的 AppShell 模式,纯 WPF(不用 WinForms):

### 6.1 启动壳 Shell(MainWindow)
- `MainWindow`(MahApps MetroWindow)即**启动壳**,承载:
  - **顶部状态条**:机器名/版本/运行状态灯(红黄绿)/急停/复位/启动/暂停/停止按钮
  - **左侧导航菜单**:主页/手动/自动/配方/报警/日志/IO/设置(按钮列表)
  - **中间 `ContentControl`**:页面承载区,`Content` 绑定 `ShellViewModel.CurrentView`
- 各页面**全部为 `UserControl`**(`Views/Pages/*.xaml` 根元素是 `UserControl`),不是 `Page`
- 导航:`ShellViewModel` 持有各页面 VM 实例(由 DI 注入),菜单命令切换 `CurrentView`;VM 内部用 DataTemplate 隐式映射到对应 UserControl(`App.Resources` 里 `DataTemplate DataType=VM → UserControl`)

### 6.2 页面(UserControl)清单

| UserControl | VM | 内容 |
|---|---|---|
| HomeView | HomeViewModel | OEE 看板(产量/良率/运行时间/报警数)+当前状态+三轴实时位置 |
| ManualView | ManualViewModel | 轴调试(JOG/回零/定位,AxisControl)+IO 手动+胶阀测试 |
| AutoView | AutoViewModel | 启动/暂停+实时步序描述(绑定 Task_Dispensing.StepDesc)+进度+产量 |
| RecipeView | RecipeViewModel | 轨迹点表格(X/Y/Z/开胶)+速度/胶量+保存/加载 JSON |
| AlarmView | AlarmViewModel | 当前报警(高亮)+历史报警查询(从 MySQL)+Ack 按钮 |
| LogView | LogViewModel | 实时日志流+按 Level/时间/模块筛选(从 MySQL 分页) |
| IoView | IoViewModel | 所有 Input/Output 指示灯(从软 PLC DB2,变色) |
| SettingView | SettingViewModel | 数据库连接串/工作模式切换/急停测试 |

### 6.3 独立弹窗画面 NotifyWindow【新增】
专用于**提示和警报信息**,独立 Window(非 UserControl):
- **`Views/Dialogs/NotifyWindow.xaml`**(MahApps MetroWindow 风格)+ `NotifyDialogViewModel`
- 实现 **`IDialogService`** 接口:
  ```
  ShowAlarm(AlarmRecord)     // 报警弹窗(红色,带 Ack 按钮)
  ShowTip(string msg)        // 操作提示(蓝色,自动消失)
  ShowConfirm(string msg)    // 确认对话框(返回 bool)
  ShowError(string msg)      // 错误弹窗(红色)
  ```
- **触发方**:
  - 报警服务 `AlarmService` 检测到新报警 → 调 `IDialogService.ShowAlarm`(同时发 AlarmEvent 让 AlarmView 列表更新)
  - 事件总线 `MessageEvent`(NotifyType=DialogNotify)→ 弹窗
  - 业务关键操作(如未初始化启动、配方保存成功)→ 提示
- 实现:用 `Show()` 模态/非模态;UI 线程调度用 `Application.Current.Dispatcher.Invoke`;同一报警去重(不重复弹)
- 严重报警(Alarm_Stop)用**模态**阻塞操作直到 Ack;Tip 用**自动延时关闭**

## 七、appsettings.json

```json
{
  "Machine": { "Name": "Dispenser-01", "Version": "V1.0.0" },
  "Hardware": { "NoHardwareMode": true, "Plc": { "Mode": "Sim" },
                "Vision": { "SimDelayMs": 500 } },
  "Database": {
    "ConnectionString": "Server=localhost;Port=3306;Database=dispenser;Uid=root;Pwd=123456;",
    "AutoCreateTable": true
  },
  "Logging": { "MinLevel": "Debug", "FilePath": "logs/log.txt" },
  "Scheduler": { "ThreadCount": 4, "TickIntervalMs": 1 }
}
```

## 八、启动流程(Bootstrapper)

读 appsettings → 配置 Serilog(File+MySQL)→ DbInitializer 建 MySQL 表 → 构建 DI(注册 IPlcHardware/Sim、IMotionCard、IGlueValve、IPressureSensor、IAlarmService、IEventBus、IDialogService、各 Repository、各页面 VM、ShellVM)→ HardwareManager.Init(启动软 PLC 扫描线程+加载轴参数)→ 注册 DataTemplate(VM→UserControl)→ 调度器 AddTask(4 个 Task)+StartTaskThread → 实例化 Shell(MainWindow) 并 Show。

## 九、实施步骤(分 7 阶段)

1. **基础骨架**:csproj NuGet 包 + Bootstrapper + DI 容器 + appsettings + Shell(MainWindow)导航框架 + DataTemplate 映射
2. **核心引擎**:TaskBase/TaskScheduler/TaskStatic/DelayerTimer/HandEvent + 事件总线 + Serilog 配置
3. **硬件仿真**:IPlcHardware + PlcSimulator(数据区+扫描循环+轴运动学)+ IMotionCard/MotionCardPlc + 胶阀/压力 Sim + HardwareManager
4. **业务任务**:Task_SystemInit / Task_Dispensing(主流程,视觉=延时)/ Task_AlarmMonitor / Task_IoWatchdog
5. **数据层**:MySQL 连接 + DbInitializer 建表 + Serilog MySQL Sink + 三个 Repository
6. **UI 各页面 + 弹窗**:8 个 UserControl 页面 VM+View + NotifyWindow 弹窗 + IDialogService,绑定 TaskStatic/Repository
7. **联调**:端到端跑通点胶流程 + 报警/日志落库 + 弹窗验证 + 空跑模式验证

## 十、特别说明

- **UI 模式**:Shell(MainWindow)+ 8 个 UserControl 页面(ContentControl 切换)+ 独立 NotifyWindow 弹窗(提示/警报),对齐 AutoStudio AppShell
- **视觉 = 延时环节**:case 30 仅 `if(Timer.Start(500)) SetStep(40)`,offset 恒 0,延时值来自 appsettings
- **MySQL 依赖**:需本机/可访问 MySQL 8.0;连接失败降级仅 File 日志并弹提示
- **PLC 软仿真**:不走真实 S7 协议,按 S7-1500 数据区建模,IPlcHardware 预留真机位

确认后立即按第 1 阶段开始落地。
