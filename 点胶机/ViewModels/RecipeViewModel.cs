using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using 点胶机.Recipe;

namespace 点胶机.ViewModels;

/// <summary>配方页 —— 轨迹点表格编辑 + 参数 + 保存/加载</summary>
public partial class RecipeViewModel : ViewModelBase
{
    private readonly RecipeManager _recipeMgr;

    public DispenseRecipe Recipe { get; }

    [ObservableProperty] private double _dispenseSpeed;
    [ObservableProperty] private double _glueFlow;

    public RecipeViewModel(RecipeManager recipeMgr)
    {
        Title = "配方";
        _recipeMgr = recipeMgr;
        Recipe = recipeMgr.Current;
        DispenseSpeed = Recipe.DispenseSpeed;
        GlueFlow = Recipe.GlueFlow;
    }

    [RelayCommand]
    private void Save()
    {
        Recipe.DispenseSpeed = DispenseSpeed;
        Recipe.GlueFlow = GlueFlow;
        _recipeMgr.Save();
        System.Windows.MessageBox.Show("配方已保存", "提示");
    }

    [RelayCommand]
    private void Reload()
    {
        _recipeMgr.Load();
        DispenseSpeed = Recipe.DispenseSpeed;
        GlueFlow = Recipe.GlueFlow;
    }

    [RelayCommand]
    private void AddPoint()
    {
        Recipe.Points.Add(new DispensePoint { X = 100, Y = 100, Z = 5, GlueOn = true });
    }

    [RelayCommand]
    private void ClearPoints()
    {
        Recipe.Points.Clear();
    }
}
