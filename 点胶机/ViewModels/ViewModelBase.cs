using CommunityToolkit.Mvvm.ComponentModel;

namespace 点胶机.ViewModels;

/// <summary>
/// 所有页面 ViewModel 的基类 —— 提供页面标题等公共属性
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>页面显示标题</summary>
    [ObservableProperty]
    private string _title = "";
}
