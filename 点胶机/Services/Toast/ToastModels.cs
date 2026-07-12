using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace 点胶机.Services.Toast;

/// <summary>通知等级(决定颜色):错误=红,警告=黄,讯息=蓝</summary>
public enum ToastLevel
{
    Info = 0,
    Warning = 1,
    Error = 2
}

/// <summary>单条通知</summary>
public class ToastItem : ObservableObject
{
    public DateTime Time { get; set; } = DateTime.Now;
    public ToastLevel Level { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";

    /// <summary>等级文字</summary>
    public string LevelText => Level switch
    {
        ToastLevel.Error => "错误",
        ToastLevel.Warning => "警告",
        _ => "讯息"
    };

    /// <summary>等级颜色(色块)</summary>
    public string LevelColor => Level switch
    {
        ToastLevel.Error => "#E53935",
        ToastLevel.Warning => "#FB8C00",
        _ => "#1565C0"
    };
}

/// <summary>Toast 服务接口 —— 供报警/业务触发通知</summary>
public interface IToastService
{
    void Info(string title, string message);
    void Warning(string title, string message);
    void Error(string title, string message);
}

/// <summary>Toast 服务实现 —— 维护通知列表,驱动 ToastWindow 显示</summary>
public sealed class ToastService : IToastService
{
    public ObservableCollection<ToastItem> Items { get; } = new();

    /// <summary>当前筛选等级(-1=全部,0=Info,1=Warning,2=Error)</summary>
    public int FilterLevel { get; set; } = -1;

    /// <summary>是否当前可见(关闭后 false,新错误/警告时重新 true)</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>最大保留条数</summary>
    private const int MaxItems = 200;

    public event Action? Changed;

    public void Info(string title, string message) => Add(ToastLevel.Info, title, message);
    public void Warning(string title, string message) => Add(ToastLevel.Warning, title, message);
    public void Error(string title, string message) => Add(ToastLevel.Error, title, message);

    private void Add(ToastLevel level, string title, string message)
    {
        // 在 UI 线程操作集合
        void DoAdd()
        {
            Items.Insert(0, new ToastItem { Level = level, Title = title, Message = message });
            // 限制条数
            while (Items.Count > MaxItems) Items.RemoveAt(Items.Count - 1);

            // 新"错误"或"警告"时,若窗口已关闭则重新弹出
            if (!IsVisible && (level == ToastLevel.Error || level == ToastLevel.Warning))
                IsVisible = true;

            Changed?.Invoke();
        }

        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
            DoAdd();
        else
            System.Windows.Application.Current?.Dispatcher?.BeginInvoke(DoAdd);
    }

    /// <summary>用户关闭窗口</summary>
    public void Close()
    {
        IsVisible = false;
        Changed?.Invoke();
    }

    /// <summary>清空全部</summary>
    public void Clear()
    {
        Items.Clear();
        Changed?.Invoke();
    }
}
