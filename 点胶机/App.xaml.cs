using System.Windows;
using System.Windows.Threading;

namespace 点胶机;

/// <summary>
/// 应用入口 —— 由 Bootstrapper 接管启动流程
/// </summary>
public partial class App : Application
{
    private Bootstrapper? _bootstrapper;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 全局异常捕获 —— 落 Serilog
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _bootstrapper = new Bootstrapper();
        await _bootstrapper.StartupAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bootstrapper?.Shutdown();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Serilog.Log.Error(e.Exception, "UI 线程未处理异常");
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Serilog.Log.Fatal(ex, "AppDomain 未处理异常");
    }

    private static void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        Serilog.Log.Error(e.Exception, "Task 未观察异常");
        e.SetObserved();
    }
}
