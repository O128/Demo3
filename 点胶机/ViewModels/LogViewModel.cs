using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using 点胶机.Data;
using 点胶机.Data.Entities;

namespace 点胶机.ViewModels;

/// <summary>日志页 —— 从 MySQL Logs 表查询 + 筛选(Level/天数)</summary>
public partial class LogViewModel : ViewModelBase
{
    private readonly LogRepository _repo;

    public ObservableCollection<LogEntity> Logs { get; } = new();

    [ObservableProperty] private string _levelFilter = "";
    [ObservableProperty] private int _filterDays = 1;
    [ObservableProperty] private string _moduleFilter = "";

    /// <summary>级别筛选选项</summary>
    public string[] LevelOptions { get; } = { "", "Error", "Warning", "Information", "Debug" };

    public LogViewModel(LogRepository repo)
    {
        Title = "日志";
        _repo = repo;
        LoadLogs();
    }

    [RelayCommand]
    private void LoadLogs()
    {
        try
        {
            var from = DateTime.Now.AddDays(-FilterDays);
            var rows = _repo.Query(from: from, level: string.IsNullOrEmpty(LevelFilter) ? null : LevelFilter,
                                   module: string.IsNullOrEmpty(ModuleFilter) ? null : ModuleFilter, limit: 1000);
            Logs.Clear();
            foreach (var r in rows) Logs.Add(r);
        }
        catch { /* MySQL 不可用时静默 */ }
    }

    [RelayCommand] private void SetLevel1Day() { FilterDays = 1; LevelFilter = ""; LoadLogs(); }
}
